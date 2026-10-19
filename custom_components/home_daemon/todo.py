"""Detect to-do items completed in Home Assistant and report them to HomeDaemon.

Home Assistant's ``todo`` entities expose their items through the entity component, not
through state attributes. A completion always changes the entity state (the state is the
number of incomplete items), so the watcher listens for ``state_changed`` on ``todo.*``
entities and diffs the completed UIDs. Newly completed UIDs are reported to HomeDaemon,
which decides whether the item belongs to one of its lists and discards it otherwise.
"""

from __future__ import annotations

import logging
from collections.abc import Iterable

from homeassistant.components.todo.const import DATA_COMPONENT
from homeassistant.components.todo.const import DOMAIN as TODO_DOMAIN
from homeassistant.const import EVENT_STATE_CHANGED
from homeassistant.core import CALLBACK_TYPE, Event, HomeAssistant, callback
from homeassistant.helpers.event import EventStateChangedData

from .api import HomeDaemonApiClient, HomeDaemonAuthError, HomeDaemonError

_LOGGER = logging.getLogger(__name__)

_COMPLETED = "completed"


def completed_uids(items: Iterable[object] | None) -> frozenset[str]:
    """Return the UIDs of the completed items, ignoring items without a UID."""
    return frozenset(
        uid
        for item in items or ()
        if (uid := getattr(item, "uid", None)) and str(getattr(item, "status", "")) == _COMPLETED
    )


def newly_completed_uids(
    previous: frozenset[str] | None, current: frozenset[str]
) -> frozenset[str]:
    """Return the UIDs that became completed since the previous reading."""
    if previous is None:
        return frozenset()
    return current - previous


class TodoCompletionWatcher:
    """Watch every ``todo.*`` entity and report completions to HomeDaemon."""

    def __init__(self, hass: HomeAssistant, client: HomeDaemonApiClient) -> None:
        self._hass = hass
        self._client = client
        self._seen: dict[str, frozenset[str]] = {}
        self._unsubscribe: CALLBACK_TYPE | None = None

    @callback
    def async_start(self) -> None:
        """Start listening for to-do item completions."""
        self._seed_existing_entities()
        self._unsubscribe = self._hass.bus.async_listen(
            EVENT_STATE_CHANGED, self._async_handle_state_changed
        )

    @callback
    def _seed_existing_entities(self) -> None:
        """Remember already-completed items so the first change can be diffed.

        Without this snapshot the first completion after a Home Assistant restart would be
        mistaken for the initial reading and ignored.
        """
        component = self._hass.data.get(DATA_COMPONENT)
        if component is None:
            return

        for entity_id in self._hass.states.async_entity_ids(TODO_DOMAIN):
            entity = component.get_entity(entity_id)
            items = getattr(entity, "todo_items", None)
            if items is not None:
                self._seen[entity_id] = completed_uids(items)

    @callback
    def async_stop(self) -> None:
        """Stop listening."""
        if self._unsubscribe is not None:
            self._unsubscribe()
            self._unsubscribe = None

    @callback
    def _async_handle_state_changed(self, event: Event[EventStateChangedData]) -> None:
        entity_id: str = event.data.get("entity_id", "")
        if not entity_id.startswith(f"{TODO_DOMAIN}."):
            return

        component = self._hass.data.get(DATA_COMPONENT)
        entity = component.get_entity(entity_id) if component is not None else None
        items = getattr(entity, "todo_items", None)
        if items is None:
            return

        current = completed_uids(items)
        previous = self._seen.get(entity_id)
        self._seen[entity_id] = current

        for uid in newly_completed_uids(previous, current):
            _LOGGER.debug("Home Assistant completed to-do item %s on %s", uid, entity_id)
            self._hass.async_create_task(
                self._async_report(entity_id, uid),
                f"home_daemon report {entity_id} {uid}",
            )

    async def _async_report(self, entity_id: str, uid: str) -> None:
        try:
            recorded = await self._client.async_report_todo_completion(entity_id, uid)
        except HomeDaemonAuthError as err:
            _LOGGER.error("HomeDaemon rejected the API key while reporting %s: %s", uid, err)
        except HomeDaemonError as err:
            _LOGGER.warning("Could not report %s on %s to HomeDaemon: %s", uid, entity_id, err)
        else:
            if not recorded:
                _LOGGER.debug("%s on %s is not a HomeDaemon item", uid, entity_id)

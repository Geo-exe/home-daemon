"""The HomeDaemon integration: health sensors, recalculation action and to-do completions."""

from __future__ import annotations

import logging
from dataclasses import dataclass

from homeassistant.config_entries import ConfigEntry
from homeassistant.const import Platform
from homeassistant.core import HomeAssistant
from homeassistant.helpers.aiohttp_client import async_get_clientsession

from .api import HomeDaemonApiClient
from .const import CONF_API_KEY, CONF_BASE_URL, DOMAIN
from .coordinator import HomeDaemonCoordinator
from .services import async_setup_services, async_unload_services
from .todo import TodoCompletionWatcher

_LOGGER = logging.getLogger(__name__)

PLATFORMS: list[Platform] = [Platform.BINARY_SENSOR, Platform.SENSOR]


@dataclass(slots=True)
class HomeDaemonRuntime:
    """Everything an entry owns while it is loaded."""

    entry_id: str
    client: HomeDaemonApiClient
    coordinator: HomeDaemonCoordinator
    todo_watcher: TodoCompletionWatcher


async def async_setup_entry(hass: HomeAssistant, entry: ConfigEntry) -> bool:
    """Set up HomeDaemon from a config entry."""
    client = HomeDaemonApiClient(
        async_get_clientsession(hass),
        entry.data[CONF_BASE_URL],
        entry.data[CONF_API_KEY],
    )
    coordinator = HomeDaemonCoordinator(hass, entry, client)
    await coordinator.async_config_entry_first_refresh()

    watcher = TodoCompletionWatcher(hass, client)
    watcher.async_start()

    hass.data.setdefault(DOMAIN, {})[entry.entry_id] = HomeDaemonRuntime(
        entry_id=entry.entry_id,
        client=client,
        coordinator=coordinator,
        todo_watcher=watcher,
    )

    await hass.config_entries.async_forward_entry_setups(entry, PLATFORMS)
    async_setup_services(hass)
    return True


async def async_unload_entry(hass: HomeAssistant, entry: ConfigEntry) -> bool:
    """Unload a config entry."""
    unloaded = await hass.config_entries.async_unload_platforms(entry, PLATFORMS)
    if not unloaded:
        return False

    runtime: HomeDaemonRuntime | None = hass.data.get(DOMAIN, {}).pop(
        entry.entry_id, None
    )
    if runtime is not None:
        runtime.todo_watcher.async_stop()

    if not hass.data.get(DOMAIN):
        async_unload_services(hass)

    return True

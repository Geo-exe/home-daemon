"""Coordinator polling HomeDaemon health and the newest scheduling run."""

from __future__ import annotations

import logging
from dataclasses import dataclass
from datetime import timedelta
from typing import Any

from homeassistant.config_entries import ConfigEntry
from homeassistant.core import HomeAssistant
from homeassistant.exceptions import ConfigEntryAuthFailed
from homeassistant.helpers.update_coordinator import DataUpdateCoordinator

from .api import (
    HomeDaemonApiClient,
    HomeDaemonAuthError,
    HomeDaemonError,
)
from .const import DEFAULT_SCAN_INTERVAL, DOMAIN

_LOGGER = logging.getLogger(__name__)


@dataclass(slots=True)
class HomeDaemonData:
    """One poll of the HomeDaemon instance."""

    reachable: bool
    ready: bool
    latest_recalculation: dict[str, Any] | None


class HomeDaemonCoordinator(DataUpdateCoordinator[HomeDaemonData]):
    """Poll health and the last recalculation.

    A connection failure is reported as data (``reachable`` is false) instead of raising
    ``UpdateFailed``: the entities then read ``off`` and keep their history, which is more
    useful for a dashboard than making everything unavailable.
    """

    def __init__(
        self,
        hass: HomeAssistant,
        entry: ConfigEntry,
        client: HomeDaemonApiClient,
    ) -> None:
        super().__init__(
            hass,
            _LOGGER,
            name=DOMAIN,
            update_interval=timedelta(seconds=DEFAULT_SCAN_INTERVAL),
            config_entry=entry,
        )
        self.client = client

    async def _async_update_data(self) -> HomeDaemonData:
        last_latest = self.data.latest_recalculation if self.data else None

        try:
            await self.client.async_check_live()
        except HomeDaemonAuthError as err:
            raise ConfigEntryAuthFailed(str(err)) from err
        except HomeDaemonError as err:
            _LOGGER.debug("HomeDaemon is unreachable: %s", err)
            return HomeDaemonData(
                reachable=False,
                ready=False,
                latest_recalculation=last_latest,
            )

        try:
            ready = await self.client.async_check_ready()
            latest = await self.client.async_get_latest_recalculation()
        except HomeDaemonAuthError as err:
            raise ConfigEntryAuthFailed(str(err)) from err
        except HomeDaemonError as err:
            _LOGGER.debug("HomeDaemon is reachable but unhealthy: %s", err)
            return HomeDaemonData(
                reachable=True,
                ready=False,
                latest_recalculation=last_latest,
            )

        return HomeDaemonData(reachable=True, ready=ready, latest_recalculation=latest)

"""Sensors exposing the outcome of the last scheduling recalculation."""

from __future__ import annotations

from typing import Any

from homeassistant.components.sensor import SensorEntity
from homeassistant.config_entries import ConfigEntry
from homeassistant.core import HomeAssistant
from homeassistant.helpers.entity_platform import AddEntitiesCallback
from homeassistant.helpers.update_coordinator import CoordinatorEntity

from .const import CONF_BASE_URL, DOMAIN, RECALCULATION_NEVER
from .coordinator import HomeDaemonCoordinator
from .entity import build_device_info


async def async_setup_entry(
    hass: HomeAssistant,
    entry: ConfigEntry,
    async_add_entities: AddEntitiesCallback,
) -> None:
    """Set up the HomeDaemon sensors."""
    coordinator: HomeDaemonCoordinator = hass.data[DOMAIN][entry.entry_id].coordinator
    async_add_entities([HomeDaemonRecalculationSensor(coordinator, entry)])


class HomeDaemonRecalculationSensor(
    CoordinatorEntity[HomeDaemonCoordinator], SensorEntity
):
    """State of the newest schedule run: never, running, succeeded or failed."""

    _attr_has_entity_name = True
    _attr_translation_key = "last_recalculation"
    _attr_icon = "mdi:calendar-sync"

    def __init__(
        self, coordinator: HomeDaemonCoordinator, entry: ConfigEntry
    ) -> None:
        super().__init__(coordinator)
        self._attr_unique_id = f"{entry.entry_id}_last_recalculation"
        self._attr_device_info = build_device_info(entry, entry.data[CONF_BASE_URL])

    @property
    def _latest(self) -> dict[str, Any] | None:
        if self.coordinator.data is None:
            return None
        return self.coordinator.data.latest_recalculation

    @property
    def native_value(self) -> str:
        """Return the run status, or ``never`` before the first run."""
        latest = self._latest
        if not latest:
            return RECALCULATION_NEVER

        status = latest.get("status")
        return str(status).lower() if status else "unknown"

    @property
    def extra_state_attributes(self) -> dict[str, Any]:
        """Expose the run details without flooding the state machine."""
        latest = self._latest
        if not latest:
            return {}

        return {
            "run_id": latest.get("id"),
            "trigger": latest.get("trigger"),
            "started_at": latest.get("startedAtUtc"),
            "completed_at": latest.get("completedAtUtc"),
            "scheduled_count": latest.get("scheduledCount"),
            "unscheduled_count": latest.get("unscheduledCount"),
            "summary": latest.get("summary"),
            "error_message": latest.get("errorMessage"),
        }

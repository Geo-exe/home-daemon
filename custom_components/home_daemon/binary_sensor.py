"""Binary sensors exposing HomeDaemon reachability and readiness."""

from __future__ import annotations

from homeassistant.components.binary_sensor import (
    BinarySensorDeviceClass,
    BinarySensorEntity,
)
from homeassistant.config_entries import ConfigEntry
from homeassistant.core import HomeAssistant
from homeassistant.helpers.entity_platform import AddEntitiesCallback
from homeassistant.helpers.update_coordinator import CoordinatorEntity

from .const import CONF_BASE_URL, DOMAIN
from .coordinator import HomeDaemonCoordinator
from .entity import build_device_info


async def async_setup_entry(
    hass: HomeAssistant,
    entry: ConfigEntry,
    async_add_entities: AddEntitiesCallback,
) -> None:
    """Set up the HomeDaemon binary sensors."""
    coordinator: HomeDaemonCoordinator = hass.data[DOMAIN][entry.entry_id].coordinator
    async_add_entities(
        [
            HomeDaemonReachableSensor(coordinator, entry),
            HomeDaemonReadySensor(coordinator, entry),
        ]
    )


class HomeDaemonBinarySensorBase(
    CoordinatorEntity[HomeDaemonCoordinator], BinarySensorEntity
):
    """Base class sharing the device and coordinator wiring."""

    _attr_has_entity_name = True
    _attr_device_class = BinarySensorDeviceClass.CONNECTIVITY

    def __init__(
        self, coordinator: HomeDaemonCoordinator, entry: ConfigEntry
    ) -> None:
        super().__init__(coordinator)
        self._attr_device_info = build_device_info(entry, entry.data[CONF_BASE_URL])


class HomeDaemonReachableSensor(HomeDaemonBinarySensorBase):
    """Whether the HomeDaemon API answers its liveness probe."""

    _attr_translation_key = "reachable"

    def __init__(
        self, coordinator: HomeDaemonCoordinator, entry: ConfigEntry
    ) -> None:
        super().__init__(coordinator, entry)
        self._attr_unique_id = f"{entry.entry_id}_reachable"

    @property
    def is_on(self) -> bool | None:
        """Return true when HomeDaemon responded to the last poll."""
        if self.coordinator.data is None:
            return None
        return self.coordinator.data.reachable


class HomeDaemonReadySensor(HomeDaemonBinarySensorBase):
    """Whether HomeDaemon reports itself ready, including its dependencies."""

    _attr_translation_key = "ready"

    def __init__(
        self, coordinator: HomeDaemonCoordinator, entry: ConfigEntry
    ) -> None:
        super().__init__(coordinator, entry)
        self._attr_unique_id = f"{entry.entry_id}_ready"

    @property
    def is_on(self) -> bool | None:
        """Return true when the readiness probe passed."""
        if self.coordinator.data is None:
            return None
        return self.coordinator.data.ready

"""Shared entity helpers for the HomeDaemon integration."""

from __future__ import annotations

from homeassistant.config_entries import ConfigEntry
from homeassistant.helpers.device_registry import DeviceInfo

from .const import DOMAIN, MANUFACTURER, MODEL


def build_device_info(entry: ConfigEntry, base_url: str) -> DeviceInfo:
    """Return the single device every HomeDaemon entity belongs to."""
    return DeviceInfo(
        identifiers={(DOMAIN, entry.entry_id)},
        name=entry.title,
        manufacturer=MANUFACTURER,
        model=MODEL,
        configuration_url=base_url,
    )

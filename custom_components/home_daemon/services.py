"""Service actions offered by the HomeDaemon integration."""

from __future__ import annotations

import logging
from typing import TYPE_CHECKING

import voluptuous as vol
from homeassistant.core import (
    HomeAssistant,
    ServiceCall,
    ServiceResponse,
    SupportsResponse,
    callback,
)
from homeassistant.exceptions import HomeAssistantError
from homeassistant.helpers import config_validation as cv

from .api import HomeDaemonError
from .const import ATTR_ENTRY_ID, DOMAIN, SERVICE_START_RECALCULATION

if TYPE_CHECKING:
    from . import HomeDaemonRuntime

_LOGGER = logging.getLogger(__name__)

START_RECALCULATION_SCHEMA = vol.Schema(
    {
        vol.Optional(ATTR_ENTRY_ID): cv.string,
    }
)


@callback
def async_setup_services(hass: HomeAssistant) -> None:
    """Register the integration's service actions once."""
    if hass.services.has_service(DOMAIN, SERVICE_START_RECALCULATION):
        return

    async def _async_start_recalculation(call: ServiceCall) -> ServiceResponse:
        runtimes: dict[str, HomeDaemonRuntime] = hass.data.get(DOMAIN, {})
        requested_entry = call.data.get(ATTR_ENTRY_ID)

        if requested_entry is not None and requested_entry not in runtimes:
            raise HomeAssistantError(
                f"HomeDaemon config entry '{requested_entry}' is not configured."
            )

        targets = (
            [runtimes[requested_entry]] if requested_entry else list(runtimes.values())
        )
        if not targets:
            raise HomeAssistantError("HomeDaemon is not configured.")

        result: dict[str, object] = {}
        for runtime in targets:
            coordinator = runtime.coordinator
            try:
                run = await runtime.client.async_start_recalculation()
            except HomeDaemonError as err:
                raise HomeAssistantError(
                    f"HomeDaemon could not start a recalculation: {err}"
                ) from err

            result[runtime.entry_id] = run
            await coordinator.async_request_refresh()

        _LOGGER.debug("Started HomeDaemon recalculation for %s", list(result))
        return result

    hass.services.async_register(
        DOMAIN,
        SERVICE_START_RECALCULATION,
        _async_start_recalculation,
        schema=START_RECALCULATION_SCHEMA,
        supports_response=SupportsResponse.OPTIONAL,
    )


@callback
def async_unload_services(hass: HomeAssistant) -> None:
    """Remove the service actions when the last entry is unloaded."""
    if hass.services.has_service(DOMAIN, SERVICE_START_RECALCULATION):
        hass.services.async_remove(DOMAIN, SERVICE_START_RECALCULATION)

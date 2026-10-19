"""Config flow for the HomeDaemon integration.

The user provides the base URL and the machine API key. The flow validates them in two
steps, mirroring how the backend protects itself: the anonymous liveness probe proves the
URL is reachable, then the authenticated handshake proves the key and identifies the
household (used as the unique id).
"""

from __future__ import annotations

from typing import Any

import voluptuous as vol
from homeassistant.config_entries import ConfigFlow, ConfigFlowResult
from homeassistant.core import HomeAssistant
from homeassistant.helpers.aiohttp_client import async_get_clientsession
from homeassistant.helpers.selector import (
    TextSelector,
    TextSelectorConfig,
    TextSelectorType,
)

from .api import (
    HomeDaemonApiClient,
    HomeDaemonAuthError,
    HomeDaemonError,
    normalize_base_url,
)
from .const import CONF_API_KEY, CONF_BASE_URL, DOMAIN

USER_SCHEMA = vol.Schema(
    {
        vol.Required(CONF_BASE_URL): TextSelector(
            TextSelectorConfig(type=TextSelectorType.URL)
        ),
        vol.Required(CONF_API_KEY): TextSelector(
            TextSelectorConfig(type=TextSelectorType.PASSWORD)
        ),
    }
)


class CannotConnect(Exception):
    """The HomeDaemon instance could not be reached."""


class InvalidAuth(Exception):
    """HomeDaemon rejected the API key."""


async def _async_validate(hass: HomeAssistant, base_url: str, api_key: str) -> dict[str, Any]:
    """Probe reachability first, then validate the key with the handshake."""
    client = HomeDaemonApiClient(async_get_clientsession(hass), base_url, api_key)

    try:
        await client.async_check_live()
    except HomeDaemonError as err:
        raise CannotConnect(str(err)) from err

    try:
        return await client.async_handshake()
    except HomeDaemonAuthError as err:
        raise InvalidAuth(str(err)) from err
    except HomeDaemonError as err:
        raise CannotConnect(str(err)) from err


class HomeDaemonConfigFlow(ConfigFlow, domain=DOMAIN):
    """Handle the HomeDaemon configuration."""

    VERSION = 1

    async def async_step_user(
        self, user_input: dict[str, Any] | None = None
    ) -> ConfigFlowResult:
        """Handle the initial configuration."""
        errors: dict[str, str] = {}

        if user_input is not None:
            try:
                base_url = normalize_base_url(user_input[CONF_BASE_URL])
            except ValueError:
                errors["base"] = "invalid_url"
            else:
                try:
                    info = await _async_validate(
                        self.hass, base_url, user_input[CONF_API_KEY]
                    )
                except InvalidAuth:
                    errors["base"] = "invalid_auth"
                except CannotConnect:
                    errors["base"] = "cannot_connect"
                except Exception:  # noqa: BLE001 - surface any unexpected failure in the UI
                    errors["base"] = "unknown"
                else:
                    await self.async_set_unique_id(str(info.get("householdId")))
                    self._abort_if_unique_id_configured()
                    return self.async_create_entry(
                        title=str(info.get("householdName") or DOMAIN),
                        data={
                            CONF_BASE_URL: base_url,
                            CONF_API_KEY: user_input[CONF_API_KEY],
                        },
                    )

        return self.async_show_form(
            step_id="user", data_schema=USER_SCHEMA, errors=errors
        )

    async def async_step_reconfigure(
        self, user_input: dict[str, Any] | None = None
    ) -> ConfigFlowResult:
        """Allow changing the URL or API key of an existing entry."""
        entry = self._get_reconfigure_entry()
        errors: dict[str, str] = {}

        if user_input is not None:
            try:
                base_url = normalize_base_url(user_input[CONF_BASE_URL])
            except ValueError:
                errors["base"] = "invalid_url"
            else:
                try:
                    info = await _async_validate(
                        self.hass, base_url, user_input[CONF_API_KEY]
                    )
                except InvalidAuth:
                    errors["base"] = "invalid_auth"
                except CannotConnect:
                    errors["base"] = "cannot_connect"
                except Exception:  # noqa: BLE001
                    errors["base"] = "unknown"
                else:
                    await self.async_set_unique_id(str(info.get("householdId")))
                    self._abort_if_unique_id_mismatch()
                    return self.async_update_reload_and_abort(
                        entry,
                        title=str(info.get("householdName") or entry.title),
                        data={
                            CONF_BASE_URL: base_url,
                            CONF_API_KEY: user_input[CONF_API_KEY],
                        },
                    )

        return self.async_show_form(
            step_id="reconfigure",
            data_schema=self.add_suggested_values_to_schema(
                USER_SCHEMA,
                {
                    CONF_BASE_URL: entry.data.get(CONF_BASE_URL),
                    CONF_API_KEY: entry.data.get(CONF_API_KEY),
                },
            ),
            errors=errors,
        )

    async def async_step_reauth(
        self, entry_data: dict[str, Any]
    ) -> ConfigFlowResult:
        """Start re-authentication after HomeDaemon rejected the key."""
        return await self.async_step_reauth_confirm()

    async def async_step_reauth_confirm(
        self, user_input: dict[str, Any] | None = None
    ) -> ConfigFlowResult:
        """Prompt for a replacement API key, keeping the stored URL."""
        entry = self._get_reauth_entry()
        errors: dict[str, str] = {}

        if user_input is not None:
            try:
                await _async_validate(
                    self.hass, entry.data[CONF_BASE_URL], user_input[CONF_API_KEY]
                )
            except InvalidAuth:
                errors["base"] = "invalid_auth"
            except CannotConnect:
                errors["base"] = "cannot_connect"
            except Exception:  # noqa: BLE001
                errors["base"] = "unknown"
            else:
                return self.async_update_reload_and_abort(
                    entry, data_updates={CONF_API_KEY: user_input[CONF_API_KEY]}
                )

        return self.async_show_form(
            step_id="reauth_confirm",
            data_schema=vol.Schema(
                {
                    vol.Required(CONF_API_KEY): TextSelector(
                        TextSelectorConfig(type=TextSelectorType.PASSWORD)
                    )
                }
            ),
            errors=errors,
        )

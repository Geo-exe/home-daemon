"""Tests for the HomeDaemon config flow."""

from __future__ import annotations

from aiohttp import ClientError
from homeassistant import config_entries
from homeassistant.core import HomeAssistant
from homeassistant.data_entry_flow import FlowResultType
from pytest_homeassistant_custom_component.common import MockConfigEntry

from custom_components.home_daemon.const import CONF_API_KEY, CONF_BASE_URL, DOMAIN

BASE_URL = "http://home-daemon.test:8080"


def _mock_success(aioclient_mock) -> None:
    """Mock the endpoints an entry uses once it is created."""
    aioclient_mock.get(f"{BASE_URL}/health/live", text="Healthy")
    aioclient_mock.get(f"{BASE_URL}/health/ready", text="Healthy")
    aioclient_mock.get(
        f"{BASE_URL}/api/v1/scheduling/recalculations?pageSize=1",
        json={"items": [], "nextCursor": None},
    )
    aioclient_mock.get(
        f"{BASE_URL}/api/v1/integrations/home-assistant/handshake",
        json={"householdId": "hh-1", "householdName": "Casa", "version": "1.0.3"},
    )


async def test_user_flow_creates_entry(
    hass: HomeAssistant, aioclient_mock
) -> None:
    _mock_success(aioclient_mock)

    result = await hass.config_entries.flow.async_init(
        DOMAIN, context={"source": config_entries.SOURCE_USER}
    )
    assert result["type"] is FlowResultType.FORM

    result = await hass.config_entries.flow.async_configure(
        result["flow_id"],
        {CONF_BASE_URL: "home-daemon.test:8080", CONF_API_KEY: "secret"},
    )

    assert result["type"] is FlowResultType.CREATE_ENTRY
    assert result["title"] == "Casa"
    assert result["data"][CONF_BASE_URL] == BASE_URL
    assert result["result"].unique_id == "hh-1"

    await hass.async_block_till_done()


async def test_user_flow_reports_invalid_auth(
    hass: HomeAssistant, aioclient_mock
) -> None:
    aioclient_mock.get(f"{BASE_URL}/health/live", text="Healthy")
    aioclient_mock.get(
        f"{BASE_URL}/api/v1/integrations/home-assistant/handshake", status=401
    )

    result = await hass.config_entries.flow.async_init(
        DOMAIN, context={"source": config_entries.SOURCE_USER}
    )
    result = await hass.config_entries.flow.async_configure(
        result["flow_id"],
        {CONF_BASE_URL: BASE_URL, CONF_API_KEY: "wrong"},
    )

    assert result["type"] is FlowResultType.FORM
    assert result["errors"] == {"base": "invalid_auth"}


async def test_user_flow_reports_cannot_connect(
    hass: HomeAssistant, aioclient_mock
) -> None:
    aioclient_mock.get(f"{BASE_URL}/health/live", exc=ClientError("boom"))

    result = await hass.config_entries.flow.async_init(
        DOMAIN, context={"source": config_entries.SOURCE_USER}
    )
    result = await hass.config_entries.flow.async_configure(
        result["flow_id"],
        {CONF_BASE_URL: BASE_URL, CONF_API_KEY: "secret"},
    )

    assert result["type"] is FlowResultType.FORM
    assert result["errors"] == {"base": "cannot_connect"}


async def test_user_flow_aborts_when_household_is_already_configured(
    hass: HomeAssistant, aioclient_mock
) -> None:
    _mock_success(aioclient_mock)

    first = await hass.config_entries.flow.async_init(
        DOMAIN, context={"source": config_entries.SOURCE_USER}
    )
    await hass.config_entries.flow.async_configure(
        first["flow_id"],
        {CONF_BASE_URL: BASE_URL, CONF_API_KEY: "secret"},
    )
    await hass.async_block_till_done()

    second = await hass.config_entries.flow.async_init(
        DOMAIN, context={"source": config_entries.SOURCE_USER}
    )
    second = await hass.config_entries.flow.async_configure(
        second["flow_id"],
        {CONF_BASE_URL: BASE_URL, CONF_API_KEY: "secret"},
    )

    assert second["type"] is FlowResultType.ABORT
    assert second["reason"] == "already_configured"


async def test_reconfigure_updates_url_and_key(
    hass: HomeAssistant, aioclient_mock
) -> None:
    _mock_success(aioclient_mock)
    entry = MockConfigEntry(
        domain=DOMAIN,
        unique_id="hh-1",
        title="Casa",
        data={CONF_BASE_URL: BASE_URL, CONF_API_KEY: "old"},
    )
    entry.add_to_hass(hass)

    result = await hass.config_entries.flow.async_init(
        DOMAIN,
        context={
            "source": config_entries.SOURCE_RECONFIGURE,
            "entry_id": entry.entry_id,
        },
    )
    result = await hass.config_entries.flow.async_configure(
        result["flow_id"],
        {CONF_BASE_URL: BASE_URL, CONF_API_KEY: "new"},
    )
    await hass.async_block_till_done()

    assert result["type"] is FlowResultType.ABORT
    assert result["reason"] == "reconfigure_successful"
    assert entry.data[CONF_API_KEY] == "new"


async def test_reauth_replaces_the_key(
    hass: HomeAssistant, aioclient_mock
) -> None:
    _mock_success(aioclient_mock)
    entry = MockConfigEntry(
        domain=DOMAIN,
        unique_id="hh-1",
        title="Casa",
        data={CONF_BASE_URL: BASE_URL, CONF_API_KEY: "old"},
    )
    entry.add_to_hass(hass)

    result = await hass.config_entries.flow.async_init(
        DOMAIN,
        context={
            "source": config_entries.SOURCE_REAUTH,
            "entry_id": entry.entry_id,
        },
        data=entry.data,
    )
    result = await hass.config_entries.flow.async_configure(
        result["flow_id"], {CONF_API_KEY: "new"}
    )
    await hass.async_block_till_done()

    assert result["type"] is FlowResultType.ABORT
    assert result["reason"] == "reauth_successful"
    assert entry.data[CONF_API_KEY] == "new"

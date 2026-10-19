"""Tests for the HomeDaemon API client."""

from __future__ import annotations

import pytest
from aiohttp import ClientConnectionError
from homeassistant.core import HomeAssistant
from homeassistant.helpers.aiohttp_client import async_get_clientsession

from custom_components.home_daemon.api import (
    HomeDaemonApiClient,
    HomeDaemonAuthError,
    HomeDaemonConnectionError,
    normalize_base_url,
)

BASE_URL = "http://home-daemon.test:8080"


def test_normalize_base_url_adds_scheme_and_strips_slashes() -> None:
    assert normalize_base_url("ha.local:8080/") == "http://ha.local:8080"
    assert normalize_base_url("https://ha.local/base/") == "https://ha.local/base"


@pytest.mark.parametrize("value", ["", "   ", "ftp://ha.local", "://nope"])
def test_normalize_base_url_rejects_invalid_values(value: str) -> None:
    with pytest.raises(ValueError):
        normalize_base_url(value)


async def test_latest_recalculation_returns_first_item(
    hass: HomeAssistant, aioclient_mock
) -> None:
    aioclient_mock.get(
        f"{BASE_URL}/api/v1/scheduling/recalculations?pageSize=1",
        json={"items": [{"id": "run-1", "status": "Succeeded"}], "nextCursor": None},
    )

    client = HomeDaemonApiClient(async_get_clientsession(hass), BASE_URL, "secret")

    assert await client.async_get_latest_recalculation() == {
        "id": "run-1",
        "status": "Succeeded",
    }


async def test_latest_recalculation_is_none_when_empty(
    hass: HomeAssistant, aioclient_mock
) -> None:
    aioclient_mock.get(
        f"{BASE_URL}/api/v1/scheduling/recalculations?pageSize=1",
        json={"items": [], "nextCursor": None},
    )

    client = HomeDaemonApiClient(async_get_clientsession(hass), BASE_URL, "secret")

    assert await client.async_get_latest_recalculation() is None


async def test_ready_probe_treats_503_as_not_ready(
    hass: HomeAssistant, aioclient_mock
) -> None:
    aioclient_mock.get(f"{BASE_URL}/health/ready", status=503)

    client = HomeDaemonApiClient(async_get_clientsession(hass), BASE_URL, "secret")

    assert await client.async_check_ready() is False


async def test_handshake_raises_on_rejected_key(
    hass: HomeAssistant, aioclient_mock
) -> None:
    aioclient_mock.get(
        f"{BASE_URL}/api/v1/integrations/home-assistant/handshake", status=403
    )

    client = HomeDaemonApiClient(async_get_clientsession(hass), BASE_URL, "wrong")

    with pytest.raises(HomeDaemonAuthError):
        await client.async_handshake()


async def test_connection_error_is_wrapped(
    hass: HomeAssistant, aioclient_mock
) -> None:
    aioclient_mock.get(f"{BASE_URL}/health/live", exc=ClientConnectionError("boom"))

    client = HomeDaemonApiClient(async_get_clientsession(hass), BASE_URL, "secret")

    with pytest.raises(HomeDaemonConnectionError):
        await client.async_check_live()


async def test_report_completion_returns_false_when_discarded(
    hass: HomeAssistant, aioclient_mock
) -> None:
    aioclient_mock.post(
        f"{BASE_URL}/api/v1/integrations/home-assistant/todo-completions", status=204
    )

    client = HomeDaemonApiClient(async_get_clientsession(hass), BASE_URL, "secret")

    assert (
        await client.async_report_todo_completion("todo.other", "uid-1") is False
    )


async def test_report_completion_returns_true_when_recorded(
    hass: HomeAssistant, aioclient_mock
) -> None:
    aioclient_mock.post(
        f"{BASE_URL}/api/v1/integrations/home-assistant/todo-completions",
        json={"choreId": "chore-1", "isDuplicate": False},
    )

    client = HomeDaemonApiClient(async_get_clientsession(hass), BASE_URL, "secret")

    assert await client.async_report_todo_completion("todo.mine", "uid-1") is True

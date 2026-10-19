"""Tests for setting up the HomeDaemon integration and its service action."""

from __future__ import annotations

from homeassistant.core import HomeAssistant
from homeassistant.helpers import entity_registry as er
from pytest_homeassistant_custom_component.common import MockConfigEntry

from custom_components.home_daemon.const import (
    CONF_API_KEY,
    CONF_BASE_URL,
    DOMAIN,
    SERVICE_START_RECALCULATION,
)

BASE_URL = "http://home-daemon.test:8080"


def _mock_endpoints(aioclient_mock, status: str = "Succeeded") -> None:
    aioclient_mock.get(f"{BASE_URL}/health/live", text="Healthy")
    aioclient_mock.get(f"{BASE_URL}/health/ready", text="Healthy")
    aioclient_mock.get(
        f"{BASE_URL}/api/v1/scheduling/recalculations?pageSize=1",
        json={
            "items": [
                {
                    "id": "run-1",
                    "trigger": "Manual",
                    "status": status,
                    "startedAtUtc": "2026-10-20T08:00:00+00:00",
                    "completedAtUtc": "2026-10-20T08:00:05+00:00",
                    "scheduledCount": 3,
                    "unscheduledCount": 1,
                    "summary": "Placed 3 chores",
                    "errorMessage": None,
                }
            ],
            "nextCursor": None,
        },
    )


async def _setup_entry(hass: HomeAssistant) -> MockConfigEntry:
    entry = MockConfigEntry(
        domain=DOMAIN,
        unique_id="hh-1",
        title="Casa",
        data={CONF_BASE_URL: BASE_URL, CONF_API_KEY: "secret"},
    )
    entry.add_to_hass(hass)
    assert await hass.config_entries.async_setup(entry.entry_id)
    await hass.async_block_till_done()
    return entry


async def test_setup_creates_health_and_recalculation_entities(
    hass: HomeAssistant, aioclient_mock
) -> None:
    _mock_endpoints(aioclient_mock)
    entry = await _setup_entry(hass)

    registry = er.async_get(hass)

    reachable = registry.async_get_entity_id(
        "binary_sensor", DOMAIN, f"{entry.entry_id}_reachable"
    )
    ready = registry.async_get_entity_id(
        "binary_sensor", DOMAIN, f"{entry.entry_id}_ready"
    )
    recalculation = registry.async_get_entity_id(
        "sensor", DOMAIN, f"{entry.entry_id}_last_recalculation"
    )

    assert hass.states.get(reachable).state == "on"
    assert hass.states.get(ready).state == "on"
    assert hass.states.get(recalculation).state == "succeeded"


async def test_recalculation_sensor_exposes_run_details(
    hass: HomeAssistant, aioclient_mock
) -> None:
    _mock_endpoints(aioclient_mock)
    entry = await _setup_entry(hass)

    registry = er.async_get(hass)
    entity_id = registry.async_get_entity_id(
        "sensor", DOMAIN, f"{entry.entry_id}_last_recalculation"
    )
    state = hass.states.get(entity_id)

    assert state.attributes["run_id"] == "run-1"
    assert state.attributes["scheduled_count"] == 3
    assert state.attributes["unscheduled_count"] == 1


async def test_start_recalculation_service_returns_the_run(
    hass: HomeAssistant, aioclient_mock
) -> None:
    _mock_endpoints(aioclient_mock)
    entry = await _setup_entry(hass)

    aioclient_mock.post(
        f"{BASE_URL}/api/v1/scheduling/recalculations",
        json={"runId": "run-2", "status": "Running"},
        status=202,
    )

    response = await hass.services.async_call(
        DOMAIN,
        SERVICE_START_RECALCULATION,
        {},
        blocking=True,
        return_response=True,
    )

    assert response[entry.entry_id] == {"runId": "run-2", "status": "Running"}

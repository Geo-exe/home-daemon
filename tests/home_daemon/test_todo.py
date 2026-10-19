"""Tests for to-do item detection and reporting."""

from __future__ import annotations

from dataclasses import dataclass
from types import SimpleNamespace
from unittest.mock import AsyncMock

from homeassistant.components.todo.const import DATA_COMPONENT
from homeassistant.const import EVENT_STATE_CHANGED
from homeassistant.core import HomeAssistant

from custom_components.home_daemon.todo import (
    TodoCompletionWatcher,
    completed_uids,
    newly_completed_uids,
)


@dataclass
class StubItem:
    uid: str | None
    status: str


def test_completed_uids_filters_open_and_uid_less_items() -> None:
    items = [
        StubItem("done", "completed"),
        StubItem("open", "needs_action"),
        StubItem(None, "completed"),
    ]

    assert completed_uids(items) == frozenset({"done"})


def test_completed_uids_handles_missing_items() -> None:
    assert completed_uids(None) == frozenset()


def test_newly_completed_uids_ignores_the_first_reading() -> None:
    assert newly_completed_uids(None, frozenset({"a"})) == frozenset()


def test_newly_completed_uids_reports_only_the_difference() -> None:
    previous = frozenset({"a"})
    current = frozenset({"a", "b"})

    assert newly_completed_uids(previous, current) == frozenset({"b"})


async def test_watcher_reports_a_new_completion(hass: HomeAssistant) -> None:
    items = [StubItem("a", "needs_action")]

    class FakeComponent:
        @staticmethod
        def get_entity(entity_id: str):
            _ = entity_id
            return SimpleNamespace(todo_items=items)

    hass.data[DATA_COMPONENT] = FakeComponent()
    hass.states.async_set("todo.test", "1")

    client = SimpleNamespace(
        async_report_todo_completion=AsyncMock(return_value=True)
    )
    watcher = TodoCompletionWatcher(hass, client)
    watcher.async_start()

    try:
        items[0].status = "completed"
        hass.bus.async_fire(EVENT_STATE_CHANGED, {"entity_id": "todo.test"})
        await hass.async_block_till_done()
    finally:
        watcher.async_stop()

    client.async_report_todo_completion.assert_awaited_once_with("todo.test", "a")

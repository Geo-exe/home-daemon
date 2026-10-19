"""Pytest fixtures for the HomeDaemon custom integration tests."""

from __future__ import annotations

import sys
from pathlib import Path

import pytest

pytest_plugins = "pytest_homeassistant_custom_component"

# Make ``custom_components.home_daemon`` importable regardless of the working directory.
REPO_ROOT = Path(__file__).parents[2]
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))


@pytest.fixture(autouse=True)
def _auto_enable_custom_integrations(enable_custom_integrations: None) -> None:
    """Load custom integrations in every test."""
    yield

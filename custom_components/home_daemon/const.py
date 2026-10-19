"""Constants for the HomeDaemon integration."""

from typing import Final

DOMAIN: Final = "home_daemon"
MANUFACTURER: Final = "HomeDaemon"
MODEL: Final = "HomeDaemon"

CONF_BASE_URL: Final = "base_url"
CONF_API_KEY: Final = "api_key"

API_KEY_HEADER: Final = "X-Api-Key"

#: How often the coordinator polls health and the last recalculation.
DEFAULT_SCAN_INTERVAL: Final = 30

#: Per-request timeout for the HomeDaemon API.
API_TIMEOUT_SECONDS: Final = 15

SERVICE_START_RECALCULATION: Final = "start_recalculation"
ATTR_ENTRY_ID: Final = "entry_id"

#: Sensor state for a household that never ran a recalculation.
RECALCULATION_NEVER: Final = "never"

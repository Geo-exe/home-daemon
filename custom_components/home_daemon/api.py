"""Thin async client for the HomeDaemon REST API.

HomeDaemon exposes three kinds of endpoint to Home Assistant:

* the anonymous health probes (``/health/live`` and ``/health/ready``);
* an authenticated handshake that proves the machine API key and names the household;
* the authenticated to-do completion callback and the scheduling recalculation endpoints.

The client maps the failures the config flow and the coordinator care about onto a small
exception hierarchy, so no caller has to inspect ``aiohttp`` internals.
"""

from __future__ import annotations

from typing import Any
from urllib.parse import urlsplit, urlunsplit

from aiohttp import ClientError, ClientSession, ClientTimeout

from .const import API_KEY_HEADER, API_TIMEOUT_SECONDS


class HomeDaemonError(Exception):
    """Base class for HomeDaemon errors."""


class HomeDaemonConnectionError(HomeDaemonError):
    """HomeDaemon could not be reached."""


class HomeDaemonAuthError(HomeDaemonError):
    """HomeDaemon rejected the machine API key."""


class HomeDaemonApiError(HomeDaemonError):
    """HomeDaemon answered with an unexpected status."""


def normalize_base_url(value: str) -> str:
    """Return a clean base URL, adding ``http://`` when no scheme was typed."""
    cleaned = value.strip()
    if not cleaned:
        raise ValueError("The base URL is empty.")

    if "://" not in cleaned:
        cleaned = f"http://{cleaned}"

    parsed = urlsplit(cleaned)
    if parsed.scheme not in ("http", "https") or not parsed.netloc:
        raise ValueError(f"'{value}' is not a valid URL.")

    path = parsed.path.rstrip("/")
    return urlunsplit((parsed.scheme, parsed.netloc, path, "", ""))


class HomeDaemonApiClient:
    """HomeDaemon API client bound to one configured household."""

    def __init__(
        self,
        session: ClientSession,
        base_url: str,
        api_key: str,
        *,
        timeout: int = API_TIMEOUT_SECONDS,
    ) -> None:
        self._session = session
        self._base_url = base_url.rstrip("/")
        self._api_key = api_key
        self._timeout = ClientTimeout(total=timeout)

    @property
    def base_url(self) -> str:
        """Return the configured base URL."""
        return self._base_url

    async def async_check_live(self) -> None:
        """Raise when the HomeDaemon process cannot be reached."""
        await self._request("GET", "health/live", authenticated=False, expect_json=False)

    async def async_check_ready(self) -> bool:
        """Return whether HomeDaemon reports itself ready, treating 503 as not ready."""
        status, _ = await self._request(
            "GET",
            "health/ready",
            authenticated=False,
            expect_json=False,
            expected_statuses=(503,),
        )
        return status == 200

    async def async_handshake(self) -> dict[str, Any]:
        """Validate the API key and return the household the instance serves."""
        _, payload = await self._request("GET", "api/v1/integrations/home-assistant/handshake")
        return payload or {}

    async def async_get_latest_recalculation(self) -> dict[str, Any] | None:
        """Return the newest schedule run, or ``None`` when none exists yet."""
        _, payload = await self._request(
            "GET",
            "api/v1/scheduling/recalculations",
            params={"pageSize": 1},
        )
        items = (payload or {}).get("items") or []
        return items[0] if items else None

    async def async_start_recalculation(self) -> dict[str, Any]:
        """Ask HomeDaemon to recalculate the schedule and return the new run."""
        _, payload = await self._request("POST", "api/v1/scheduling/recalculations", json={})
        return payload or {}

    async def async_report_todo_completion(
        self,
        todo_list_entity_id: str,
        item_uid: str,
        *,
        completed_by_member_id: str | None = None,
        idempotency_key: str | None = None,
    ) -> bool:
        """Report a completed Home Assistant to-do item.

        Returns ``True`` when HomeDaemon recorded a completion for one of its items and
        ``False`` when the item belongs to a list HomeDaemon does not manage.
        """
        body: dict[str, Any] = {
            "todoListEntityId": todo_list_entity_id,
            "itemUid": item_uid,
        }
        if completed_by_member_id:
            body["completedByMemberId"] = completed_by_member_id
        if idempotency_key:
            body["idempotencyKey"] = idempotency_key

        status, _ = await self._request(
            "POST",
            "api/v1/integrations/home-assistant/todo-completions",
            json=body,
            expected_statuses=(204,),
        )
        return status == 200

    async def _request(
        self,
        method: str,
        path: str,
        *,
        authenticated: bool = True,
        params: dict[str, Any] | None = None,
        json: dict[str, Any] | None = None,
        expect_json: bool = True,
        expected_statuses: tuple[int, ...] = (),
    ) -> tuple[int, dict[str, Any] | None]:
        headers = {API_KEY_HEADER: self._api_key} if authenticated else None
        url = f"{self._base_url}/{path.lstrip('/')}"

        try:
            async with self._session.request(
                method,
                url,
                headers=headers,
                params=params,
                json=json,
                timeout=self._timeout,
            ) as response:
                if response.status in (401, 403):
                    raise HomeDaemonAuthError(
                        f"HomeDaemon rejected the API key (HTTP {response.status})."
                    )

                if response.status in expected_statuses:
                    return response.status, None

                if response.status >= 400:
                    detail = (await response.text())[:200]
                    raise HomeDaemonApiError(
                        f"HomeDaemon returned HTTP {response.status} for '{path}': {detail}"
                    )

                if response.status == 204:
                    return response.status, None

                if not expect_json:
                    return response.status, None

                try:
                    payload = await response.json(content_type=None)
                except ValueError as err:
                    raise HomeDaemonApiError(
                        f"HomeDaemon returned an invalid JSON payload for '{path}'."
                    ) from err

                return response.status, payload if isinstance(payload, dict) else None
        except (TimeoutError, ClientError) as err:
            raise HomeDaemonConnectionError(
                f"Could not reach HomeDaemon at '{self._base_url}': {err}"
            ) from err

// Controller module for configPage.html, loaded by Jellyfin's SPA via
// data-controller="__plugin/jellybirdjs" (see Plugin.cs — a second
// PluginPageInfo named "jellybirdjs" pointing at this file). This is the
// officially supported way to run JS on a plugin page: Jellyfin dynamic-
// imports this as an ES module and calls its default export with the
// page's view element. An inline <script> tag in the HTML itself is only
// executed via a jQuery-specific DOM-insertion path that isn't reliably
// exercised — see the removed <script> block's comment in configPage.html.
const PluginId = "23ba68d8-75e0-4f79-b5ea-348ad0e3214f";

export default function (view) {
    // --- Tabs ------------------------------------------------------

    function showTab(tab) {
        view.querySelector("#JbTabSettings").style.display = tab === "settings" ? "" : "none";
        view.querySelector("#JbTabSearch").style.display = tab === "search" ? "" : "none";
    }

    view.querySelector("#JbTabSettingsBtn").addEventListener("click", function () { showTab("settings"); });
    view.querySelector("#JbTabSearchBtn").addEventListener("click", function () { showTab("search"); });
    showTab("search"); // Search is the default landing tab, not Settings.

    // --- Settings ----------------------------------------------------

    function normalizeBaseUrl(value) {
        return (value || "").trim().replace(/\/+$/, "");
    }

    function isValidHttpUrl(value) {
        try {
            const u = new URL(value);
            return u.protocol === "http:" || u.protocol === "https:";
        } catch {
            return false;
        }
    }

    view.addEventListener("viewshow", function () {
        Dashboard.showLoadingMsg();
        ApiClient.getPluginConfiguration(PluginId).then(function (config) {
            view.querySelector("#JellybirdBaseUrl").value = config.JellybirdBaseUrl || "";
            view.querySelector("#JellybirdToken").value = config.JellybirdToken || "";
            view.querySelector("#DefaultProvider").value = config.DefaultProvider || "";
            Dashboard.hideLoadingMsg();
        });
    });

    view.querySelector("#TestConnectionButton").addEventListener("click", function () {
        const resultEl = view.querySelector("#TestConnectionResult");
        const baseUrl = normalizeBaseUrl(view.querySelector("#JellybirdBaseUrl").value);
        const token = view.querySelector("#JellybirdToken").value || "";

        if (!isValidHttpUrl(baseUrl)) {
            resultEl.textContent = "Enter a valid http:// or https:// base URL first.";
            resultEl.style.color = "#cc4444";
            return;
        }

        resultEl.textContent = "Testing…";
        resultEl.style.color = "";

        const url = ApiClient.getUrl("Jellybird/TestConnection", { baseUrl: baseUrl, token: token });
        ApiClient.getJSON(url).then(function (health) {
            resultEl.textContent = "Reachable — jellybird " + (health.version || "");
            resultEl.style.color = "#4caf50";
        }).catch(function () {
            resultEl.textContent = "Unreachable — check the base URL and token.";
            resultEl.style.color = "#cc4444";
        });
    });

    view.querySelector("#JellybirdConfigForm").addEventListener("submit", function (e) {
        e.preventDefault();
        Dashboard.showLoadingMsg();

        const baseUrl = normalizeBaseUrl(view.querySelector("#JellybirdBaseUrl").value);
        if (baseUrl && !isValidHttpUrl(baseUrl)) {
            Dashboard.hideLoadingMsg();
            Dashboard.alert("Jellybird base URL must be a valid http:// or https:// URL.");
            return false;
        }

        ApiClient.getPluginConfiguration(PluginId).then(function (config) {
            config.JellybirdBaseUrl = baseUrl;
            config.JellybirdToken = view.querySelector("#JellybirdToken").value || "";
            config.DefaultProvider = view.querySelector("#DefaultProvider").value || "";
            ApiClient.updatePluginConfiguration(PluginId, config).then(function (result) {
                Dashboard.processPluginConfigurationUpdateResult(result);
            });
        });

        return false;
    });

    // --- Search / Add --------------------------------------------------

    const state = {
        selected: null,
        stack: [],
    };

    const panels = ["JbResults", "JbSeasons", "JbEpisodes", "JbTorrents"];

    function showPanel(id) {
        panels.forEach(function (p) {
            view.querySelector("#" + p).style.display = p === id ? "" : "none";
        });
        view.querySelector("#JbBreadcrumb").style.display = state.stack.length ? "" : "none";
    }

    function pushPanel(fromId, toId) {
        state.stack.push(fromId);
        showPanel(toId);
    }

    view.querySelector("#JbBackButton").addEventListener("click", function () {
        const prev = state.stack.pop();
        if (prev) {
            showPanel(prev);
        }
    });

    function setStatus(text, isError) {
        const el = view.querySelector("#JbStatus");
        el.textContent = text || "";
        el.style.color = isError ? "#cc4444" : "";
    }

    function apiGet(path, params) {
        const url = ApiClient.getUrl(path, params || {});
        return ApiClient.getJSON(url).catch(function (err) {
            const message = (err && err.error) || "Request failed — check jellybird connectivity in plugin settings.";
            setStatus(message, true);
            throw err;
        });
    }

    // Best-effort "already in library" check — a failure (e.g. jellybird
    // briefly unreachable) just means no badge, not a visible error; it
    // doesn't go through apiGet() since it shouldn't ever populate the
    // status line for a background label check.
    function checkExists(mediaType, tmdbId, season, episode) {
        const params = { tmdbId: tmdbId, type: mediaType };
        if (season !== undefined) params.season = season;
        if (episode !== undefined) params.episode = episode;
        const url = ApiClient.getUrl("Jellybird/Library/Check", params);
        return ApiClient.getJSON(url).catch(function () { return false; });
    }

    function posterUrl(path) {
        return path ? ("https://image.tmdb.org/t/p/w200" + path) : "";
    }

    // Episode stills are landscape (16:9), unlike posters — a wider TMDB
    // size looks right scaled down to the thumbnail box in episodeRow().
    function stillUrl(path) {
        return path ? ("https://image.tmdb.org/t/p/w300" + path) : "";
    }

    function formatBytes(bytes) {
        if (!bytes) return "?";
        const units = ["B", "KB", "MB", "GB", "TB"];
        let i = 0;
        let n = bytes;
        while (n >= 1024 && i < units.length - 1) {
            n /= 1024;
            i++;
        }
        return n.toFixed(1) + " " + units[i];
    }

    function card(imgSrc, title, subtitle, onClick) {
        const div = document.createElement("div");
        div.className = "card";
        div.style.cssText = "display:inline-block;width:150px;margin:0.5em;cursor:pointer;vertical-align:top;";
        div.innerHTML =
            '<div style="background:#222;height:220px;display:flex;align-items:center;justify-content:center;overflow:hidden;">' +
            (imgSrc ? '<img src="' + imgSrc + '" style="width:100%;height:100%;object-fit:cover;" />' : '<span>No image</span>') +
            '</div>' +
            '<div style="font-weight:bold;margin-top:0.3em;">' + title + '</div>' +
            '<div style="opacity:0.7;font-size:0.9em;">' + (subtitle || "") + '</div>';
        div.addEventListener("click", onClick);
        return div;
    }

    function row(title, subtitle, buttonText, onButtonClick) {
        const div = document.createElement("div");
        div.style.cssText = "display:flex;align-items:center;justify-content:space-between;padding:0.6em;border-bottom:1px solid #333;";
        const left = document.createElement("div");
        left.innerHTML = '<div>' + title + '</div><div style="opacity:0.7;font-size:0.9em;">' + subtitle + '</div>';
        div.appendChild(left);
        if (buttonText) {
            const btn = document.createElement("button");
            btn.setAttribute("is", "emby-button");
            btn.className = "raised";
            btn.textContent = buttonText;
            btn.addEventListener("click", onButtonClick);
            div.appendChild(btn);
        }
        return div;
    }

    function episodeRow(imgSrc, title, subtitle, buttonText, onClick) {
        const div = document.createElement("div");
        div.style.cssText = "display:flex;align-items:center;gap:0.8em;padding:0.6em;border-bottom:1px solid #333;cursor:pointer;";
        div.innerHTML =
            '<div style="flex:0 0 120px;width:120px;height:68px;background:#222;border-radius:4px;overflow:hidden;display:flex;align-items:center;justify-content:center;">' +
            (imgSrc ? '<img src="' + imgSrc + '" style="width:100%;height:100%;object-fit:cover;" />' : '<span style="font-size:0.75em;opacity:0.5;">No image</span>') +
            '</div>' +
            '<div style="flex:1;min-width:0;">' +
            '<div style="font-weight:bold;">' + title + '</div>' +
            '<div style="opacity:0.7;font-size:0.9em;">' + (subtitle || "") + '</div>' +
            '</div>';
        if (buttonText) {
            const btn = document.createElement("button");
            btn.setAttribute("is", "emby-button");
            btn.className = "raised";
            btn.textContent = buttonText;
            btn.style.flex = "0 0 auto";
            div.appendChild(btn);
        }
        div.addEventListener("click", onClick);
        return div;
    }

    function runSearch() {
        const q = view.querySelector("#JbSearchInput").value.trim();
        if (!q) {
            return;
        }
        state.stack = [];
        setStatus("Searching…");
        apiGet("Jellybird/Search", { q: q }).then(function (results) {
            renderResults(results || []);
            setStatus((results || []).length ? "" : "No results.");
            showPanel("JbResults");
        });
    }

    function renderResults(results) {
        const container = view.querySelector("#JbResults");
        container.innerHTML = "";
        results.forEach(function (r) {
            const title = r.media_type === "tv" ? (r.name || r.original_name) : (r.title || r.original_title);
            const dateStr = r.media_type === "tv" ? r.first_air_date : r.release_date;
            const year = dateStr ? dateStr.substring(0, 4) : "";
            const subtitle = (r.media_type === "tv" ? "TV" : "Movie") + (year ? " · " + year : "");
            const cardEl = card(posterUrl(r.poster_path), title, subtitle, function () {
                state.selected = r;
                if (r.media_type === "tv") {
                    loadSeasons(r.id);
                } else {
                    loadTorrents(r.id, "movie", null, null);
                }
            });
            container.appendChild(cardEl);
            // TV shows aren't checked here — "in library" only makes sense
            // per episode (or a whole season pack), which needs a season
            // picked first; see the badge added in loadEpisodes() instead.
            if (r.media_type !== "tv") {
                checkExists("movie", r.id).then(function (exists) {
                    if (!exists) return;
                    const badge = document.createElement("div");
                    badge.style.cssText = "margin-top:0.3em;color:#4caf50;font-size:0.85em;font-weight:bold;";
                    badge.textContent = "In library";
                    cardEl.appendChild(badge);
                });
            }
        });
    }

    view.querySelector("#JbSearchButton").addEventListener("click", runSearch);
    view.querySelector("#JbSearchInput").addEventListener("keydown", function (e) {
        if (e.key === "Enter") {
            runSearch();
        }
    });

    function loadSeasons(tmdbId) {
        setStatus("Loading seasons…");
        apiGet("Jellybird/Tv/Seasons", { tmdbId: tmdbId }).then(function (seasons) {
            const container = view.querySelector("#JbSeasons");
            container.innerHTML = "";
            (seasons || []).forEach(function (s) {
                container.appendChild(card(posterUrl(s.poster_path), s.name, s.episode_count + " episodes", function () {
                    loadEpisodes(tmdbId, s.season_number);
                }));
            });
            setStatus("");
            pushPanel("JbResults", "JbSeasons");
        });
    }

    function loadEpisodes(tmdbId, season) {
        setStatus("Loading episodes…");
        apiGet("Jellybird/Tv/Episodes", { tmdbId: tmdbId, season: season }).then(function (episodes) {
            const container = view.querySelector("#JbEpisodes");
            container.innerHTML = "";
            (episodes || []).forEach(function (ep) {
                const title = "E" + ep.episode_number + " · " + (ep.name || "");
                const subtitle = [ep.air_date, ep.overview].filter(Boolean).join(" — ");
                const rowEl = episodeRow(stillUrl(ep.still_path), title, subtitle, "Select", function () {
                    loadTorrents(tmdbId, "tv", season, ep.episode_number);
                });
                container.appendChild(rowEl);
                checkExists("tv", tmdbId, season, ep.episode_number).then(function (exists) {
                    if (!exists) return;
                    const badge = document.createElement("span");
                    badge.style.cssText = "color:#4caf50;font-size:0.85em;font-weight:bold;margin-left:0.5em;";
                    badge.textContent = "In library";
                    // rowEl.children[0] is the thumbnail, [1] is the text
                    // container, whose [0] is the bold title line.
                    rowEl.children[1].children[0].appendChild(badge);
                });
            });
            setStatus((episodes || []).length ? "" : "No episodes found.");
            pushPanel("JbSeasons", "JbEpisodes");
        });
    }

    function loadTorrents(tmdbId, mediaType, season, episode) {
        setStatus("Searching torrents — this can take a few seconds…");
        const params = { tmdbId: tmdbId, type: mediaType };
        if (season !== null && season !== undefined) params.season = season;
        if (episode !== null && episode !== undefined) params.episode = episode;

        apiGet("Jellybird/Torrents", params).then(function (torrents) {
            const container = view.querySelector("#JbTorrents");
            container.innerHTML = "";
            (torrents || []).forEach(function (t) {
                const cachedBadge = t.cached ? ("Cached (" + t.provider + ")") : "Not cached";
                const subtitle = formatBytes(t.size_bytes) + " · " + t.seeders + " seeders · " + t.source + " · " + cachedBadge;
                container.appendChild(row(t.title, subtitle, "Add", function () {
                    addTorrent(t, mediaType, season, episode);
                }));
            });
            setStatus((torrents || []).length ? "" : "No torrents found.");
            pushPanel(mediaType === "tv" ? "JbEpisodes" : "JbResults", "JbTorrents");
        });
    }

    function addTorrent(torrent, mediaType, season, episode) {
        setStatus("Adding…");
        const dateStr = state.selected ? (mediaType === "tv" ? state.selected.first_air_date : state.selected.release_date) : "";
        const year = dateStr && dateStr.length >= 4 ? parseInt(dateStr.substring(0, 4), 10) : undefined;
        const body = {
            magnet: torrent.magnet,
            info_hash: torrent.hash,
            provider: torrent.provider || undefined,
            title: state.selected ? (state.selected.title || state.selected.name) : undefined,
            year: year,
            media_type: mediaType,
            season: season || undefined,
            episode: episode || undefined,
            tmdb_id: state.selected ? String(state.selected.id) : undefined,
        };
        const url = ApiClient.getUrl("Jellybird/Add");
        ApiClient.ajax({ type: "POST", url: url, data: JSON.stringify(body), contentType: "application/json" })
            .then(function (response) { return response.json(); })
            .then(function (result) {
                const msg = result.cached
                    ? "Added — already cached, sync triggered."
                    : "Added — will appear once it finishes downloading.";
                setStatus(msg, false);
            })
            .catch(function () {
                setStatus("Add failed — see jellybird logs for details.", true);
            });
    }
}

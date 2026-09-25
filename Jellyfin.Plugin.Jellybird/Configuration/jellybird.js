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

    const tabs = {
        discover: "JbTabDiscover",
        search: "JbTabSearch",
        cloud: "JbTabCloud",
        local: "JbTabLocal",
        settings: "JbTabSettings",
    };
    let currentTab = null;

    // Each tab's onShow/onHide are defined further down, next to that
    // tab's code; showTab only runs once they all exist (see the end of
    // this function).
    const tabHooks = {};

    function showTab(tab) {
        if (currentTab && tabHooks[currentTab] && tabHooks[currentTab].hide) {
            tabHooks[currentTab].hide();
        }
        currentTab = tab;
        Object.keys(tabs).forEach(function (t) {
            view.querySelector("#" + tabs[t]).style.display = t === tab ? "" : "none";
        });
        view.querySelectorAll("[data-tab]").forEach(function (btn) {
            btn.classList.toggle("button-submit", btn.getAttribute("data-tab") === tab);
        });
        // Discover and Search share the results/drill-down area below them.
        view.querySelector("#JbBrowse").style.display = tab === "discover" || tab === "search" ? "" : "none";
        if (tabHooks[tab] && tabHooks[tab].show) {
            tabHooks[tab].show();
        }
    }

    view.querySelectorAll("[data-tab]").forEach(function (btn) {
        btn.addEventListener("click", function () { showTab(btn.getAttribute("data-tab")); });
    });

    // --- Shared helpers ------------------------------------------------

    // esc HTML-escapes a value before it goes into innerHTML. Titles,
    // torrent names and error messages all come from outside (TMDB,
    // indexers, debrid) and this page runs with an admin session, so
    // everything interpolated into markup must go through here.
    function esc(value) {
        return String(value === null || value === undefined ? "" : value).replace(/[&<>"'`]/g, function (c) {
            return { "&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;", "'": "&#39;", "`": "&#96;" }[c];
        });
    }

    const fallbackError = "Request failed — check jellybird connectivity in plugin settings.";

    // ApiClient rejects with the fetch Response on HTTP errors; the
    // controller always sends {"error": "..."}, so dig the message out.
    function errorText(err) {
        if (err && typeof err.json === "function") {
            return err.json().then(function (body) { return (body && body.error) || fallbackError; }, function () { return fallbackError; });
        }
        return Promise.resolve((err && (err.error || err.message)) || fallbackError);
    }

    // apiCall runs a Jellybird/* controller request and resolves with the
    // parsed JSON, or rejects with an Error carrying jellybird's message.
    function apiCall(method, path, params, body) {
        const request = { type: method, url: ApiClient.getUrl(path, params || {}) };
        if (body !== undefined) {
            request.data = JSON.stringify(body);
            request.contentType = "application/json";
        }
        return ApiClient.ajax(request)
            .then(function (response) { return response.json(); })
            .catch(function (err) {
                return errorText(err).then(function (message) { throw new Error(message); });
            });
    }

    function statusLine(el, text, isError) {
        el.textContent = text || "";
        el.style.color = isError ? "#cc4444" : "";
    }

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
        // The results panel the drill-down started from: JbResults
        // (Search) or JbDiscoverResults (Discover).
        home: "JbResults",
    };

    const panels = ["JbResults", "JbDiscoverResults", "JbSeasons", "JbEpisodes", "JbTorrents"];

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
        statusLine(view.querySelector("#JbStatus"), text, isError);
    }

    function apiGet(path, params) {
        return apiCall("GET", path, params).catch(function (err) {
            setStatus(err.message, true);
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
            (imgSrc ? '<img src="' + esc(imgSrc) + '" loading="lazy" style="width:100%;height:100%;object-fit:cover;" />' : '<span>No image</span>') +
            '</div>' +
            '<div style="font-weight:bold;margin-top:0.3em;">' + esc(title) + '</div>' +
            '<div style="opacity:0.7;font-size:0.9em;">' + esc(subtitle) + '</div>';
        div.addEventListener("click", onClick);
        return div;
    }

    function row(title, subtitle, buttonText, onButtonClick) {
        const div = document.createElement("div");
        div.style.cssText = "display:flex;align-items:center;justify-content:space-between;padding:0.6em;border-bottom:1px solid #333;";
        const left = document.createElement("div");
        left.innerHTML = '<div>' + esc(title) + '</div><div style="opacity:0.7;font-size:0.9em;">' + esc(subtitle) + '</div>';
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
            (imgSrc ? '<img src="' + esc(imgSrc) + '" style="width:100%;height:100%;object-fit:cover;" />' : '<span style="font-size:0.75em;opacity:0.5;">No image</span>') +
            '</div>' +
            '<div style="flex:1;min-width:0;">' +
            '<div style="font-weight:bold;">' + esc(title) + '</div>' +
            '<div style="opacity:0.7;font-size:0.9em;">' + esc(subtitle) + '</div>' +
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
        state.home = "JbResults";
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
            pushPanel(state.home, "JbSeasons");
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
            pushPanel(mediaType === "tv" ? "JbEpisodes" : state.home, "JbTorrents");
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
                if (result.cached && state.selected) {
                    markDiscoverAdded(mediaType, state.selected.id);
                }
            })
            .catch(function (err) {
                errorText(err).then(function (message) {
                    setStatus("Add failed: " + message, true);
                });
            });
    }

    tabHooks.search = {
        show: function () {
            state.stack = [];
            state.home = "JbResults";
            setStatus("");
            showPanel("JbResults");
        },
    };

    // --- Discover ------------------------------------------------------

    const discover = {
        type: "movie",
        list: "trending",
        genre: "",
        page: 0,
        totalPages: 1,
        loading: false,
        listsLoaded: null, // media type whose lists/genres are in the selects
        cards: new Map(), // "type:id" -> {el, item}
        hide: false,
    };

    try {
        discover.hide = localStorage.getItem("jellybird.discover.hide") === "1";
    } catch (e) {
        // Storage can be blocked; the toggle just won't be remembered.
    }
    view.querySelector("#JbDiscoverHide").checked = discover.hide;

    function discoverBadge(item) {
        if (!item.in_library) return "";
        const eps = item.media_type === "tv" && item.episodes ? " · " + item.episodes + " ep" + (item.episodes === 1 ? "" : "s") : "";
        return "In library" + eps;
    }

    function setDiscoverBadge(cardEl, item) {
        let badge = cardEl.querySelector(".jb-have");
        const text = discoverBadge(item);
        if (!text) {
            if (badge) badge.remove();
            return;
        }
        if (!badge) {
            badge = document.createElement("div");
            badge.className = "jb-have";
            badge.style.cssText = "margin-top:0.3em;color:#4caf50;font-size:0.85em;font-weight:bold;";
            cardEl.appendChild(badge);
        }
        badge.textContent = text;
        const img = cardEl.querySelector("img");
        if (img) img.style.opacity = "0.55";
    }

    function renderDiscoverItem(item) {
        const title = item.media_type === "tv" ? (item.name || item.original_name) : (item.title || item.original_title);
        const dateStr = item.media_type === "tv" ? item.first_air_date : item.release_date;
        const year = dateStr ? dateStr.substring(0, 4) : "";
        const rating = item.vote_average ? "★ " + item.vote_average.toFixed(1) : "";
        const subtitle = [year, rating].filter(Boolean).join(" · ");
        const cardEl = card(posterUrl(item.poster_path), title || "Unknown", subtitle, function () {
            state.selected = item;
            state.stack = [];
            state.home = "JbDiscoverResults";
            if (item.media_type === "tv") {
                loadSeasons(item.id);
            } else {
                loadTorrents(item.id, "movie", null, null);
            }
        });
        cardEl.title = item.overview || "";
        setDiscoverBadge(cardEl, item);
        cardEl.style.display = discover.hide && item.in_library ? "none" : "inline-block";
        discover.cards.set(item.media_type + ":" + item.id, { el: cardEl, item: item });
        return cardEl;
    }

    function updateDiscoverCount() {
        const all = Array.from(discover.cards.values());
        const have = all.filter(function (c) { return c.item.in_library; }).length;
        view.querySelector("#JbDiscoverCount").textContent = all.length
            ? all.length + " titles · " + have + " already in your library" + (discover.hide && have ? " (hidden)" : "")
            : "";
    }

    // markDiscoverAdded relabels a Discover card after a cached add, so
    // it doesn't keep looking missing until the list is reloaded.
    function markDiscoverAdded(mediaType, tmdbId) {
        const c = discover.cards.get(mediaType + ":" + tmdbId);
        if (!c) return;
        if (mediaType === "tv") {
            c.item.episodes = (c.item.episodes || 0) + 1;
        }
        c.item.in_library = true;
        setDiscoverBadge(c.el, c.item);
        updateDiscoverCount();
    }

    function loadDiscoverLists() {
        const type = discover.type;
        setStatus("Loading…");
        return apiGet("Jellybird/Discover/Lists", { type: type }).then(function (res) {
            if (type !== discover.type) return false;
            const lists = res.lists || [];
            const genres = res.genres || [];
            if (!lists.some(function (l) { return l.name === discover.list; })) {
                discover.list = lists.length ? lists[0].name : "trending";
            }
            view.querySelector("#JbDiscoverList").innerHTML = lists.map(function (l) {
                return '<option value="' + esc(l.name) + '"' + (l.name === discover.list ? " selected" : "") + ">" + esc(l.label) + "</option>";
            }).join("");
            discover.genre = "";
            view.querySelector("#JbDiscoverGenre").innerHTML = '<option value="">Any genre</option>' + genres.map(function (g) {
                return '<option value="' + esc(g.id) + '">' + esc(g.name) + "</option>";
            }).join("");
            view.querySelector("#JbDiscoverList").disabled = false;
            discover.listsLoaded = type;
            return true;
        });
    }

    function resetDiscover() {
        discover.page = 0;
        discover.totalPages = 1;
        discover.cards.clear();
        view.querySelector("#JbDiscoverGrid").innerHTML = "";
        updateDiscoverCount();
        state.stack = [];
        showPanel("JbDiscoverResults");
        loadMoreDiscover();
    }

    // loadMoreDiscover appends the next page. With "hide what I have" on
    // a whole page can come back hidden, so it keeps going (a few pages at
    // most) until something new is visible.
    function loadMoreDiscover() {
        if (discover.loading) return;
        discover.loading = true;
        view.querySelector("#JbDiscoverMore").style.display = "none";
        setStatus("Loading…");
        const want = { type: discover.type, list: discover.list, genre: discover.genre };
        let shown = 0;
        let tries = 0;

        function stale() {
            return want.type !== discover.type || want.list !== discover.list || want.genre !== discover.genre;
        }

        function next() {
            if (tries >= 5 || shown >= 8 || discover.page >= discover.totalPages) {
                return Promise.resolve();
            }
            tries++;
            const params = { type: want.type, page: discover.page + 1 };
            if (want.genre) params.genre = want.genre; else params.list = want.list;
            return apiGet("Jellybird/Discover", params).then(function (res) {
                if (stale()) return;
                discover.page = res.page || discover.page + 1;
                discover.totalPages = res.total_pages || discover.page;
                const grid = view.querySelector("#JbDiscoverGrid");
                (res.results || []).forEach(function (item) {
                    // TMDB pages overlap as rankings shift between requests.
                    if (discover.cards.has(item.media_type + ":" + item.id)) return;
                    const el = renderDiscoverItem(item);
                    grid.appendChild(el);
                    if (el.style.display !== "none") shown++;
                });
                return next();
            });
        }

        next().then(function () {
            if (stale()) return;
            setStatus(discover.cards.size ? "" : "Nothing here.");
            updateDiscoverCount();
        }).catch(function () {
            // apiGet already put the error in the status line.
        }).then(function () {
            discover.loading = false;
            if (stale()) {
                resetDiscover();
                return;
            }
            view.querySelector("#JbDiscoverMore").style.display = discover.page < discover.totalPages ? "" : "none";
        });
    }

    tabHooks.discover = {
        show: function () {
            state.stack = [];
            state.home = "JbDiscoverResults";
            setStatus("");
            showPanel("JbDiscoverResults");
            if (discover.listsLoaded !== discover.type) {
                loadDiscoverLists().then(function (ok) { if (ok) resetDiscover(); }).catch(function () {});
            } else {
                updateDiscoverCount();
            }
        },
    };

    view.querySelector("#JbDiscoverType").addEventListener("change", function (e) {
        discover.type = e.target.value;
        loadDiscoverLists().then(function (ok) { if (ok) resetDiscover(); }).catch(function () {});
    });
    view.querySelector("#JbDiscoverList").addEventListener("change", function (e) {
        discover.list = e.target.value;
        resetDiscover();
    });
    view.querySelector("#JbDiscoverGenre").addEventListener("change", function (e) {
        discover.genre = e.target.value;
        // A genre browses TMDB's discover endpoint, which has no named lists.
        view.querySelector("#JbDiscoverList").disabled = !!discover.genre;
        resetDiscover();
    });
    view.querySelector("#JbDiscoverHide").addEventListener("change", function (e) {
        discover.hide = e.target.checked;
        try {
            localStorage.setItem("jellybird.discover.hide", discover.hide ? "1" : "0");
        } catch (err) {
            // See above: not remembered, still applied.
        }
        let visible = 0;
        discover.cards.forEach(function (c) {
            const hidden = discover.hide && c.item.in_library;
            c.el.style.display = hidden ? "none" : "inline-block";
            if (!hidden) visible++;
        });
        updateDiscoverCount();
        if (discover.hide && !visible) loadMoreDiscover();
    });
    view.querySelector("#JbDiscoverMore").addEventListener("click", loadMoreDiscover);

    // --- Cloud ---------------------------------------------------------

    let cloudData = [];

    function actionButton(text, onClick) {
        const btn = document.createElement("button");
        btn.setAttribute("is", "emby-button");
        btn.type = "button";
        btn.className = "raised";
        btn.textContent = text;
        btn.addEventListener("click", function (e) {
            e.stopPropagation();
            onClick(btn);
        });
        return btn;
    }

    function listRow(titleHtml, subtitleHtml, buttons) {
        const div = document.createElement("div");
        div.style.cssText = "display:flex;align-items:center;justify-content:space-between;gap:0.8em;padding:0.6em;border-bottom:1px solid #333;";
        const left = document.createElement("div");
        left.style.cssText = "flex:1;min-width:0;overflow-wrap:anywhere;";
        left.innerHTML = "<div>" + titleHtml + '</div><div style="opacity:0.7;font-size:0.9em;">' + subtitleHtml + "</div>";
        div.appendChild(left);
        const right = document.createElement("div");
        right.style.cssText = "display:flex;gap:0.4em;flex:0 0 auto;";
        buttons.forEach(function (b) { right.appendChild(b); });
        div.appendChild(right);
        return div;
    }

    function renderCloud() {
        const container = view.querySelector("#JbCloudList");
        const filter = view.querySelector("#JbCloudFilter").value.trim().toLowerCase();
        const rows = cloudData.filter(function (t) { return !filter || t.name.toLowerCase().indexOf(filter) !== -1; });
        container.innerHTML = "";
        if (!rows.length) {
            container.innerHTML = '<p style="opacity:0.7;">' + (cloudData.length ? "No matches." : "Your debrid cloud is empty.") + "</p>";
            return;
        }
        rows.forEach(function (t) {
            const subtitle = [t.provider, t.status, t.files + " file" + (t.files === 1 ? "" : "s"), formatBytes(t.size)].map(esc).join(" · ");
            const buttons = [];
            if (t.status === "ready") {
                buttons.push(actionButton("Keep local", function (btn) {
                    btn.disabled = true;
                    const status = view.querySelector("#JbCloudStatus");
                    apiCall("POST", "Jellybird/Local", null, { provider: t.provider, torrent_id: t.id }).then(function (res) {
                        statusLine(status, "Queued " + res.queued + " of " + res.files + " file(s) from " + t.name + " — see Local files.");
                    }).catch(function (err) {
                        statusLine(status, t.name + ": " + err.message, true);
                        btn.disabled = false;
                    });
                }));
            }
            buttons.push(actionButton("Delete", function (btn) {
                if (!window.confirm("Delete \"" + t.name + "\" from your " + t.provider + " cloud? This removes it there and from the library. Local copies on the server are kept.")) {
                    return;
                }
                btn.disabled = true;
                const status = view.querySelector("#JbCloudStatus");
                apiCall("DELETE", "Jellybird/Cloud", { provider: t.provider, id: t.id }).then(function () {
                    cloudData = cloudData.filter(function (x) { return x !== t; });
                    statusLine(status, "Deleted " + t.name + ".");
                    renderCloud();
                }).catch(function (err) {
                    statusLine(status, t.name + ": " + err.message, true);
                    btn.disabled = false;
                });
            }));
            container.appendChild(listRow(esc(t.name), subtitle, buttons));
        });
    }

    function loadCloud() {
        const status = view.querySelector("#JbCloudStatus");
        statusLine(status, "Loading…");
        apiCall("GET", "Jellybird/Cloud").then(function (list) {
            cloudData = (list || []).sort(function (a, b) { return a.name.localeCompare(b.name); });
            statusLine(status, cloudData.length + " torrent(s)");
            renderCloud();
        }).catch(function (err) {
            statusLine(status, err.message, true);
        });
    }

    tabHooks.cloud = { show: loadCloud };
    view.querySelector("#JbCloudRefresh").addEventListener("click", loadCloud);
    view.querySelector("#JbCloudFilter").addEventListener("input", renderCloud);

    // --- Local files ---------------------------------------------------

    let localTimer = null;

    function isActive(lf) {
        return lf.status === "queued" || lf.status === "downloading" || lf.status === "moving";
    }

    function localStatusText(lf) {
        switch (lf.status) {
            case "done": return '<span style="color:#4caf50;">local ✓</span>';
            case "queued": return "queued";
            case "downloading":
            case "moving": {
                const pct = lf.size_bytes ? Math.floor(lf.bytes_done * 100 / lf.size_bytes) : 0;
                return '<span style="color:#ffc861;">' + (lf.status === "moving" ? "moving " : "downloading ") + pct + "%</span>";
            }
            case "failed": return '<span style="color:#cc4444;">failed</span>' + (lf.error ? " — " + esc(lf.error) : "");
        }
        return esc(lf.status);
    }

    function localAction(text, lf, run, confirmText) {
        return actionButton(text, function (btn) {
            if (confirmText && !window.confirm(confirmText)) return;
            btn.disabled = true;
            run().then(loadLocal).catch(function (err) {
                statusLine(view.querySelector("#JbLocalStatus"), fileName(lf) + ": " + err.message, true);
                btn.disabled = false;
            });
        });
    }

    function fileName(lf) {
        return (lf.local_path || lf.file_path).split("/").pop();
    }

    function renderLocal(list) {
        const container = view.querySelector("#JbLocalList");
        container.innerHTML = "";
        if (!list.length) {
            container.innerHTML = '<p style="opacity:0.7;">Nothing saved locally yet — use <strong>Keep local</strong> on the Cloud tab.</p>';
            return;
        }
        list.forEach(function (lf) {
            const ref = { provider: lf.provider, torrent_id: lf.torrent_id, file_id: lf.file_id };
            const del = function () {
                return apiCall("DELETE", "Jellybird/Local", { provider: lf.provider, torrentId: lf.torrent_id, fileId: lf.file_id });
            };
            const buttons = [];
            if (isActive(lf) && lf.status !== "moving") {
                buttons.push(localAction("Cancel", lf, del));
            } else if (lf.status === "failed") {
                buttons.push(localAction("Retry", lf, function () { return apiCall("POST", "Jellybird/Local", null, ref); }));
                buttons.push(localAction("Forget", lf, del));
            } else if (lf.status === "done" || lf.status === "moving") {
                if (lf.move_to && lf.status === "done") {
                    buttons.push(localAction("Move", lf, function () { return apiCall("POST", "Jellybird/Local/Move", null, ref); },
                        "Move this file to " + lf.move_to + "? It keeps playing from the old location until the copy finishes."));
                }
                buttons.push(localAction("Remove", lf, del, lf.in_library
                    ? "Delete the local copy from the server? The title goes back to streaming from your debrid."
                    : "This is the ONLY copy left — the title is gone from your debrid cloud. Delete it from the server for good?"));
            }
            const size = lf.status === "downloading" || lf.status === "moving"
                ? formatBytes(lf.bytes_done) + " / " + formatBytes(lf.size_bytes)
                : formatBytes(lf.size_bytes);
            const notes = (lf.in_library ? "" : ' · <span style="color:#8fb4ff;">only copy — gone from cloud</span>') +
                (lf.move_to ? ' · <span style="color:#ffc861;">in library folder</span>' : "");
            const subtitle = [esc(lf.provider), localStatusText(lf), esc(size)].join(" · ") + notes +
                '<div style="font-size:0.85em;">' + esc(lf.torrent_name) + "</div>";
            container.appendChild(listRow(esc(fileName(lf)), subtitle, buttons));
        });
    }

    function loadLocal() {
        clearTimeout(localTimer);
        const status = view.querySelector("#JbLocalStatus");
        return apiCall("GET", "Jellybird/Local").then(function (list) {
            list = list || [];
            const done = list.filter(function (lf) { return lf.status === "done"; });
            const active = list.filter(isActive).length;
            statusLine(status, done.length + " saved (" + formatBytes(done.reduce(function (n, lf) { return n + lf.size_bytes; }, 0)) + ")" +
                (active ? " · " + active + " in progress" : ""));
            renderLocal(list);
            // Poll while something is moving, slower otherwise — only while
            // this tab is showing (the hide hook clears the timer).
            if (currentTab === "local") {
                localTimer = setTimeout(loadLocal, active ? 3000 : 20000);
            }
        }).catch(function (err) {
            statusLine(status, err.message, true);
        });
    }

    tabHooks.local = {
        show: loadLocal,
        hide: function () { clearTimeout(localTimer); },
    };
    view.querySelector("#JbLocalRefresh").addEventListener("click", loadLocal);
    view.addEventListener("viewhide", function () { clearTimeout(localTimer); });

    showTab("discover"); // Discover is the landing tab.
}

(function () {
    const headers = { "Etag": 1, "Last-Modified": 1, "Content-Length": 1, "Content-Type": 1 },
        resources = {},
        pendingRequests = {},
        currentLinkElements = {},
        oldLinkElements = {},
        interval = 1000;
    let loaded = false,
        active = { "html": 1, "css": 1, "js": 1 };

    const Live = {
        heartbeat: function () {
            if (document.body) {
                if (!loaded) Live.loadresources();
                Live.checkForChanges();
            }
            setTimeout(Live.heartbeat, interval);
        },

        loadresources: function () {
            function isLocal(url) {
                const loc = document.location,
                    reg = new RegExp("^\\.|^\/(?!\/)|^[\\w]((?!://).)*$|" + loc.protocol + "//" + loc.host);
                return url.match(reg);
            }

            const scripts = document.getElementsByTagName("script"),
                links = document.getElementsByTagName("link"),
                uris = [];

            for (let script of scripts) {
                const src = script.getAttribute("src");
                if (src && isLocal(src)) uris.push(src);
                if (src && src.includes('live.js#')) {
                    for (let type in active)
                        active[type] = src.includes("[#,|]" + type);
                    if (src.includes("notify")) alert("Live.js is loaded.");
                }
            }
            if (!active.js) uris = [];
            if (active.html) uris.push(document.location.href);

            for (let link of links) {
                if (active.css) {
                    const rel = link.getAttribute("rel"),
                        href = link.getAttribute("href", 2);
                    if (href && rel && rel.toLowerCase().includes("stylesheet") && isLocal(href)) {
                        uris.push(href);
                        currentLinkElements[href] = link;
                    }
                }
            }

            for (let url of uris) {
                Live.getHead(url, function (url, info) {
                    resources[url] = info;
                });
            }

            const head = document.getElementsByTagName("head")[0],
                style = document.createElement("style"),
                rule = "transition: all .3s ease-out;",
                css = [".livejs-loading * { ", rule, " -webkit-", rule, "-moz-", rule, "-o-", rule, "}"].join('');
            style.setAttribute("type", "text/css");
            head.appendChild(style);
            style.styleSheet ? style.styleSheet.cssText = css : style.appendChild(document.createTextNode(css));

            loaded = true;
        },

        checkForChanges: function () {
            for (let url in resources) {
                if (pendingRequests[url]) continue;

                Live.getHead(url, function (url, newInfo) {
                    const oldInfo = resources[url];
                    let hasChanged = false;
                    resources[url] = newInfo;

                    for (let header in oldInfo) {
                        const oldValue = oldInfo[header],
                            newValue = newInfo[header],
                            contentType = newInfo["Content-Type"];

                        switch (header.toLowerCase()) {
                            case "etag":
                                if (!newValue) break;
                            default:
                                hasChanged = oldValue != newValue;
                                break;
                        }
                        if (hasChanged) {
                            Live.refreshResource(url, contentType);
                            break;
                        }
                    }
                });
            }
        },

        refreshResource: function (url, type) {
            switch (type.toLowerCase()) {
                case "text/css":
                    const link = currentLinkElements[url],
                        html = document.body.parentNode,
                        head = link.parentNode,
                        next = link.nextSibling,
                        newLink = document.createElement("link");

                    html.className += ' livejs-loading';
                    newLink.setAttribute("type", "text/css");
                    newLink.setAttribute("rel", "stylesheet");
                    newLink.setAttribute("href", url + "?now=" + new Date().getTime());
                    next ? head.insertBefore(newLink, next) : head.appendChild(newLink);
                    currentLinkElements[url] = newLink;
                    oldLinkElements[url] = link;

                    Live.removeoldLinkElements();
                    break;

                case "text/html":
                    if (url != document.location.href) return;
                case "text/javascript":
                case "application/javascript":
                case "application/x-javascript":
                    document.location.reload();
                    break;
            }
        },

        removeoldLinkElements: function () {
            let pending = 0;
            for (let url in oldLinkElements) {
                try {
                    const link = currentLinkElements[url],
                        oldLink = oldLinkElements[url],
                        html = document.body.parentNode,
                        sheet = link.sheet || link.styleSheet,
                        rules = sheet.rules || sheet.cssRules;

                    if (rules.length >= 0) {
                        oldLink.parentNode.removeChild(oldLink);
                        delete oldLinkElements[url];
                        setTimeout(function () {
                            html.className = html.className.replace(/\s*livejs\-loading/gi, '');
                        }, 100);
                    }
                } catch (e) {
                    pending++;
                }
                if (pending) setTimeout(Live.removeoldLinkElements, 50);
            }
        },

        getHead: function (url, callback) {
            pendingRequests[url] = true;
            const xhr = window.XMLHttpRequest ? new XMLHttpRequest() : new ActiveXObject("Microsoft.XmlHttp");
            xhr.open("HEAD", url, true);
            xhr.onreadystatechange = function () {
                delete pendingRequests[url];
                if (xhr.readyState == 4 && xhr.status != 304) {
                    let info = {};
                    for (let h in headers) {
                        let value = xhr.getResponseHeader(h);
                        if (h.toLowerCase() == "etag" && value) value = value.replace(/^W\//, '');
                        if (h.toLowerCase() == "content-type" && value) value = value.replace(/^(.*?);.*?$/i, "$1");
                        info[h] = value;
                    }
                    callback(url, info);
                }
            }
            xhr.send();
        }
    };

    if (document.location.protocol != "file:") {
        if (!window.liveJsLoaded) Live.heartbeat();
        window.liveJsLoaded = true;
    } else if (window.console) {
        console.log("Live.js doesn't support the file protocol. It needs http.");
    }
})();

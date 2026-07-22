(function (window, document) {
    'use strict';

    // Keep the zoom preview dependency-free until a visitor interacts with it.

    if (window.SzaipaMagnifyLoader?.bind) {
        return;
    }

    var imageSelector = '.zoom[data-magnify-src]';
    var interactionEvents = ['pointermove', 'mousemove', 'focusin', 'touchstart'];
    var boundImages = new WeakSet();
    var imageStates = new WeakMap();
    var libraryPromise = null;
    var resourceElements = Object.create(null);

    var resources = {
        css: {
            tagName: 'link',
            urlAttribute: 'href',
            url: '/Content/Model/css/magnify.css',
            configure: function (element) {
                element.rel = 'stylesheet';
            },
            isReady: function (element) {
                return Boolean(element?.sheet);
            },
        },
        jquery: {
            tagName: 'script',
            urlAttribute: 'src',
            url: '/Content/Model/jquery-3.6.1.min.js',
            configure: function (element) {
                element.async = true;
            },
            isReady: function () {
                return Boolean(window.jQuery?.fn);
            },
        },
        plugin: {
            tagName: 'script',
            urlAttribute: 'src',
            url: '/Content/Model/jquery.magnify.js',
            configure: function (element) {
                element.async = true;
            },
            isReady: function () {
                return typeof window.jQuery?.fn?.magnify === 'function';
            },
        },
    };

    function toAbsoluteUrl(url) {
        return new URL(url, document.baseURI).href;
    }

    function findResourceElement(resourceKey, resource) {
        if (resourceElements[resourceKey]) {
            return resourceElements[resourceKey];
        }

        var markedElement = document.querySelector(
            '[data-szaipa-magnify-resource="' + resourceKey + '"]'
        );
        if (markedElement) {
            resourceElements[resourceKey] = markedElement;
            return markedElement;
        }

        var expectedUrl = toAbsoluteUrl(resource.url);
        var candidates = document.querySelectorAll(
            resource.tagName + '[' + resource.urlAttribute + ']'
        );

        for (var index = 0; index < candidates.length; index += 1) {
            var candidate = candidates[index];
            if (toAbsoluteUrl(candidate.getAttribute(resource.urlAttribute)) === expectedUrl) {
                resourceElements[resourceKey] = candidate;
                return candidate;
            }
        }

        return null;
    }

    function createResourceElement(resourceKey, resource) {
        var element = document.createElement(resource.tagName);
        element.setAttribute('data-szaipa-magnify-resource', resourceKey);
        resource.configure(element);
        resourceElements[resourceKey] = element;
        return element;
    }

    function discardResourceElement(resourceKey, element) {
        element.szaipaMagnifyLoadPromise = null;
        if (element.isConnected) {
            element.remove();
        }

        if (resourceElements[resourceKey] === element) {
            delete resourceElements[resourceKey];
        }
    }

    function loadResource(resourceKey) {
        var resource = resources[resourceKey];
        var element = findResourceElement(resourceKey, resource);

        if (resource.isReady(element)) {
            return Promise.resolve(element);
        }

        if (!element) {
            element = createResourceElement(resourceKey, resource);
        }

        if (element.szaipaMagnifyLoadPromise) {
            return element.szaipaMagnifyLoadPromise;
        }

        if (element.isConnected && document.readyState !== 'loading') {
            // A same-URL node with no working runtime and no in-flight promise
            // has already finished unsuccessfully. Replace it on this attempt;
            // reassigning src/href on an errored node is unreliable in browsers.
            discardResourceElement(resourceKey, element);
            element = createResourceElement(resourceKey, resource);
        }

        var shouldAppend = !element.isConnected;

        element.dataset.szaipaMagnifyState = 'loading';
        element.szaipaMagnifyLoadPromise = new Promise(function (resolve, reject) {
            function cleanUp() {
                element.removeEventListener('load', onLoad);
                element.removeEventListener('error', onError);
            }

            function fail() {
                cleanUp();
                element.dataset.szaipaMagnifyState = 'failed';
                discardResourceElement(resourceKey, element);
                reject(new Error('Unable to load Magnify resource: ' + resource.url));
            }

            function onLoad() {
                if (!resource.isReady(element)) {
                    fail();
                    return;
                }

                cleanUp();
                element.dataset.szaipaMagnifyState = 'loaded';
                resolve(element);
            }

            function onError() {
                fail();
            }

            element.addEventListener('load', onLoad);
            element.addEventListener('error', onError);

            if (resource.isReady(element)) {
                onLoad();
                return;
            }

            if (shouldAppend) {
                element.setAttribute(resource.urlAttribute, resource.url);
                document.head.appendChild(element);
            }
        });

        return element.szaipaMagnifyLoadPromise;
    }

    function ensureLibraries() {
        if (libraryPromise) {
            return libraryPromise;
        }

        var attempt = Promise.all([
            loadResource('css'),
            loadResource('jquery'),
        ]).then(function () {
            return loadResource('plugin');
        }).then(function () {
            if (typeof window.jQuery?.fn?.magnify !== 'function') {
                throw new Error('Magnify plugin did not initialize.');
            }

            return window.jQuery;
        });

        libraryPromise = attempt.catch(function (error) {
            libraryPromise = null;
            throw error;
        });

        return libraryPromise;
    }

    function removeInteractionListeners(image) {
        interactionEvents.forEach(function (eventName) {
            image.removeEventListener(eventName, onFirstInteraction);
        });
    }

    function initializeImage(image) {
        var currentState = imageStates.get(image);
        if (currentState?.status === 'ready') {
            return Promise.resolve();
        }

        if (currentState?.status === 'loading') {
            return currentState.promise;
        }

        var magnifySource = image.getAttribute('data-magnify-src');
        if (!magnifySource) {
            return Promise.resolve();
        }

        var initializationPromise = ensureLibraries().then(function (jquery) {
            if (!image.isConnected) {
                imageStates.delete(image);
                return;
            }

            jquery(image).magnify({
                speed: 200,
                src: magnifySource,
                magnifiedWidth: 1000,
                magnifiedHeight: 1000,
            });

            imageStates.set(image, { status: 'ready' });
            removeInteractionListeners(image);
        }).catch(function () {
            // Keep the original preview/link and the interaction listeners. A
            // later interaction can retry the shared libraries and this image.
            imageStates.delete(image);
        });

        imageStates.set(image, {
            status: 'loading',
            promise: initializationPromise,
        });

        return initializationPromise;
    }

    function onFirstInteraction(event) {
        initializeImage(event.currentTarget);
    }

    function bindImage(image) {
        if (boundImages.has(image)) {
            return;
        }

        boundImages.add(image);
        interactionEvents.forEach(function (eventName) {
            image.addEventListener(eventName, onFirstInteraction, { passive: true });
        });
    }

    function bind(root) {
        var scope = root && typeof root.querySelectorAll === 'function'
            ? root
            : document;

        if (scope.matches?.(imageSelector)) {
            bindImage(scope);
        }

        scope.querySelectorAll(imageSelector).forEach(bindImage);
    }

    window.SzaipaMagnifyLoader = { bind: bind };
}(window, document));

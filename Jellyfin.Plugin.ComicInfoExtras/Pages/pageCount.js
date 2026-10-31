// ComicInfo Extras: shows the page count of books in the info line of the details page,
// where movies show their runtime. Loaded into the web client by the plugin.
(function () {
    'use strict';

    var CLASS_NAME = 'comicInfoExtrasPageCount';
    // Item id -> page count (number) or null when the item has none. Promises while loading.
    var cache = {};
    var scheduled = false;

    function getItemId() {
        var query = window.location.hash.split('?')[1] || window.location.search.slice(1);
        return new URLSearchParams(query).get('id');
    }

    function isDetailsRoute() {
        var route = window.location.hash.replace(/^#!?/, '').split('?')[0] || window.location.pathname;
        return /(^|\/)details$/i.test(route);
    }

    function formatPages(count) {
        var lang = (document.documentElement.lang || navigator.language || 'en').toLowerCase();
        var number = new Intl.NumberFormat(lang).format(count);
        if (lang.indexOf('de') === 0) {
            return number + (count === 1 ? ' Seite' : ' Seiten');
        }

        return number + (count === 1 ? ' page' : ' pages');
    }

    function loadPageCount(itemId) {
        if (!(itemId in cache)) {
            var client = window.ApiClient;
            cache[itemId] = client.getJSON(client.getUrl('ComicInfoExtras/Items/' + itemId + '/PageCount'))
                .then(function (result) {
                    cache[itemId] = result && result.PageCount > 0 ? result.PageCount : null;
                    return cache[itemId];
                }, function () {
                    // 404: not a book, or the page count is unknown.
                    cache[itemId] = null;
                    return null;
                });
        }

        return Promise.resolve(cache[itemId]);
    }

    function insert(container, text) {
        var existing = container.querySelector('.' + CLASS_NAME);
        if (existing) {
            if (existing.textContent !== text) {
                existing.textContent = text;
            }

            return;
        }

        var element = document.createElement('div');
        element.className = 'mediaInfoItem ' + CLASS_NAME;
        element.textContent = text;

        // Same spot as a movie's runtime: after the year, before rating badges and stars.
        var before = container.querySelector('.mediaInfoOfficialRating, .starRatingContainer, .mediaInfoCriticRating, .endsAt');
        container.insertBefore(element, before && before.parentNode === container ? before : null);
        container.classList.remove('hide');
    }

    function update() {
        scheduled = false;

        if (!window.ApiClient || !isDetailsRoute()) {
            return;
        }

        var page = document.querySelector('#itemDetailPage:not(.hide)');
        var itemId = getItemId();
        if (!page || !itemId) {
            return;
        }

        var containers = page.querySelectorAll('.itemMiscInfo-primary');
        if (!containers.length) {
            return;
        }

        loadPageCount(itemId).then(function (count) {
            // The user may have moved on while the request was running.
            if (count === null || getItemId() !== itemId) {
                return;
            }

            var text = formatPages(count);
            containers.forEach(function (container) {
                insert(container, text);
            });
        });
    }

    function schedule() {
        if (!scheduled) {
            scheduled = true;
            window.requestAnimationFrame(update);
        }
    }

    // The details page re-renders the info line whenever the item or its user data changes.
    new MutationObserver(schedule).observe(document.documentElement, { childList: true, subtree: true });
    window.addEventListener('hashchange', schedule);
    schedule();
})();

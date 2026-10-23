// Shared pager/sort/filter behaviour for the account and user list pages.
// Each page calls ListView.init(config) with its own selectors and ids.
(function () {
    'use strict';

    function init(config) {
        var pageSizeSelect = document.getElementById(config.pageSizeSelectId);
        var pageSize = config.defaultPageSize > 0 ? config.defaultPageSize : 50;
        if (pageSizeSelect) {
            var initial = parseInt(pageSizeSelect.value, 10);
            if (initial > 0) { pageSize = initial; }
        }

        var currentPage = 1;
        var searchInput = document.getElementById(config.searchInputId);
        var pageInfo = document.getElementById(config.pageInfoId);
        var pageOfTemplate = pageInfo ? pageInfo.getAttribute('data-template') : '';
        var noMatchHint = document.getElementById(config.noMatchHintId);

        var headers = Array.prototype.slice.call(document.querySelectorAll(config.sortHeaderSelector));
        var numericKeys = headers.filter(function (th) {
            return th.getAttribute('data-sort-type') === 'number';
        }).map(function (th) {
            return th.getAttribute('data-sort-key');
        });

        var desktopRows = Array.prototype.slice.call(document.querySelectorAll(config.desktopRowSelector));
        var mobileRows = Array.prototype.slice.call(document.querySelectorAll(config.mobileRowSelector));
        var allRows = desktopRows.map(function (dr, i) {
            var row = {
                desktop: dr,
                mobile: mobileRows[i],
                search: (dr.getAttribute('data-search') || '').toLowerCase()
            };
            headers.forEach(function (th) {
                var key = th.getAttribute('data-sort-key');
                if (!key) { return; }
                var attr = dr.getAttribute('data-' + key);
                row[key] = numericKeys.indexOf(key) !== -1
                    ? (parseInt(attr, 10) || 0)
                    : (attr || '').toLowerCase();
            });
            return row;
        });
        var filteredRows = allRows;
        var sortKey = null;
        var sortDir = 1;

        function totalPages() { return Math.max(1, Math.ceil(filteredRows.length / pageSize)); }

        function applySort() {
            if (!sortKey) { return; }
            var numeric = numericKeys.indexOf(sortKey) !== -1;
            filteredRows.sort(function (a, b) {
                var x = a[sortKey], y = b[sortKey];
                if (numeric) { return (x - y) * sortDir; }
                return String(x).localeCompare(String(y)) * sortDir;
            });

            // The table body and the card list hold the same items in the same order, and the
            // pager pairs them by position, so both have to be reordered together.
            var tbody = document.getElementById(config.tableBodyId);
            var cards = document.getElementById(config.cardListId);
            filteredRows.forEach(function (row) {
                if (tbody) { tbody.appendChild(row.desktop); }
                if (cards && row.mobile) { cards.appendChild(row.mobile); }
            });

            headers.forEach(function (th) {
                var el = th.querySelector('.sort-indicator');
                if (el) { el.className = 'sort-indicator'; }
            });
            var active = document.querySelector(config.sortHeaderSelector + '[data-sort-key="' + sortKey + '"] .sort-indicator');
            if (active) {
                active.className = 'sort-indicator bi ' + (sortDir === 1 ? 'bi-arrow-up' : 'bi-arrow-down');
            }
        }

        function renderPage() {
            var start = (currentPage - 1) * pageSize;
            var end = start + pageSize;
            allRows.forEach(function (row) {
                row.desktop.style.display = 'none';
                row.mobile.style.display = 'none';
            });
            filteredRows.forEach(function (row, idx) {
                if (idx >= start && idx < end) {
                    row.desktop.style.display = '';
                    row.mobile.style.display = '';
                }
            });
        }

        function renderPagination() {
            var ul = document.getElementById(config.paginationId);
            var card = document.getElementById(config.paginationCardId);
            var pages = totalPages();
            if (pages <= 1) {
                card.style.display = 'none';
                ul.innerHTML = '';
                return;
            }
            card.style.display = '';
            var html = '';
            if (currentPage > 1) {
                html += '<li class="page-item"><a class="page-link" href="#" data-page="' + (currentPage - 1) + '">' + config.prevText + '</a></li>';
            }
            var startPage = Math.max(1, currentPage - 1);
            var endPage = Math.min(pages, currentPage + 1);
            for (var i = startPage; i <= endPage; i++) {
                html += '<li class="page-item ' + (i === currentPage ? 'active' : '') + '"><a class="page-link" href="#" data-page="' + i + '">' + i + '</a></li>';
            }
            if (currentPage < pages) {
                html += '<li class="page-item"><a class="page-link" href="#" data-page="' + (currentPage + 1) + '">' + config.nextText + '</a></li>';
            }
            ul.innerHTML = html;
        }

        function updateSummary() {
            var totalEl = document.getElementById(config.totalId);
            if (totalEl) { totalEl.textContent = filteredRows.length; }
            if (pageInfo && pageOfTemplate) {
                pageInfo.textContent = pageOfTemplate.replace('{PAGE}', currentPage).replace('{TOTAL}', totalPages());
            }
        }

        function updateNoMatchHint() {
            if (noMatchHint) {
                noMatchHint.style.display = (filteredRows.length === 0 && allRows.length > 0) ? '' : 'none';
            }
        }

        function applyFilter() {
            var term = (searchInput.value || '').trim().toLowerCase();
            if (!term) {
                filteredRows = allRows;
            } else {
                filteredRows = allRows.filter(function (row) {
                    return row.search.indexOf(term) !== -1;
                });
            }
            currentPage = 1;
            applySort();
            renderPage();
            renderPagination();
            updateSummary();
            updateNoMatchHint();
        }

        var pendingFrame = null;
        if (searchInput) {
            searchInput.addEventListener('input', function () {
                if (pendingFrame) { cancelAnimationFrame(pendingFrame); }
                pendingFrame = requestAnimationFrame(applyFilter);
            });
        }

        var paginationUl = document.getElementById(config.paginationId);
        if (paginationUl) {
            paginationUl.addEventListener('click', function (e) {
                var link = e.target.closest('.page-link');
                if (!link) { return; }
                e.preventDefault();
                var page = parseInt(link.getAttribute('data-page'), 10);
                if (isNaN(page) || page < 1 || page > totalPages()) { return; }
                currentPage = page;
                renderPage();
                renderPagination();
                updateSummary();
                window.scrollTo({ top: 0, behavior: 'smooth' });
            });
        }

        headers.forEach(function (th) {
            th.addEventListener('click', function () {
                var key = th.getAttribute('data-sort-key');
                sortDir = (sortKey === key) ? -sortDir : 1;
                sortKey = key;
                currentPage = 1;
                applySort();
                renderPage();
                renderPagination();
                updateSummary();
            });
        });

        if (pageSizeSelect) {
            pageSizeSelect.addEventListener('change', function () {
                var value = parseInt(pageSizeSelect.value, 10);
                if (!value || value < 1) { return; }
                pageSize = value;
                currentPage = 1;
                renderPage();
                renderPagination();
                updateSummary();
            });
        }

        // The handlers above only run on input or on a click, so without this first pass the
        // list stays exactly as the server rendered it: every row visible, no pagination built,
        // and a "page 1 of N" label that nothing has updated. It looks like a single page
        // holding everything, and typing into the search box is what makes paging appear.
        applyFilter();
    }

    window.ListView = { init: init };
})();

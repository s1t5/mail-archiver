// Spaltenbreiten in Tabellen per Drag anpassen.
// Die Breiten leben nur im DOM und werden bewusst nicht gespeichert:
// Nach einem Neuladen steht die Tabelle wieder in ihrer natürlichen Breite.
(function () {
    'use strict';

    var MIN_COLUMN_WIDTH = 48;

    function measureHeaders(table) {
        return Array.prototype.map.call(table.tHead.rows[0].cells, function (cell) {
            return cell.getBoundingClientRect().width;
        });
    }

    function startResize(event, table, columnIndex) {
        if (event.button !== 0) {
            return;
        }

        event.preventDefault();
        event.stopPropagation();

        var cols = Array.prototype.slice.call(table.querySelectorAll('colgroup > col'));
        if (cols.length < columnIndex + 2) {
            return;
        }

        // Aktuell gerenderte Breiten übernehmen, damit ein vorheriges Strecken
        // der Tabelle (min-width: 100%) den Drag nicht verfälscht.
        var actual = measureHeaders(table);
        var total = actual.reduce(function (a, b) { return a + b; }, 0);
        actual.forEach(function (width, index) {
            if (cols[index]) {
                cols[index].style.width = width + 'px';
            }
        });
        table.style.width = total + 'px';

        var startX = event.clientX;
        var startLeft = actual[columnIndex];
        var startRight = actual[columnIndex + 1];
        var pairWidth = startLeft + startRight;
        var min = Math.min(MIN_COLUMN_WIDTH, pairWidth / 2);
        var handle = event.currentTarget;

        handle.classList.add('is-active');
        document.body.classList.add('col-resizing');

        if (handle.setPointerCapture) {
            try {
                handle.setPointerCapture(event.pointerId);
            } catch (e) {
                // Ohne Pointer Capture funktioniert der Drag nur innerhalb des Griffs.
            }
        }

        function onMove(moveEvent) {
            var delta = moveEvent.clientX - startX;
            var left = Math.max(min, Math.min(startLeft + delta, pairWidth - min));
            cols[columnIndex].style.width = left + 'px';
            cols[columnIndex + 1].style.width = (pairWidth - left) + 'px';
        }

        function onEnd(endEvent) {
            handle.removeEventListener('pointermove', onMove);
            handle.removeEventListener('pointerup', onEnd);
            handle.removeEventListener('pointercancel', onEnd);
            handle.classList.remove('is-active');
            document.body.classList.remove('col-resizing');

            if (handle.releasePointerCapture && endEvent.pointerId !== undefined) {
                try {
                    handle.releasePointerCapture(endEvent.pointerId);
                } catch (e) {
                    // Capture war bereits freigegeben.
                }
            }
        }

        handle.addEventListener('pointermove', onMove);
        handle.addEventListener('pointerup', onEnd);
        handle.addEventListener('pointercancel', onEnd);
    }

    function initTable(table) {
        if (table.dataset.columnResizeReady === 'true') {
            return;
        }
        if (!table.tHead || !table.tHead.rows.length) {
            return;
        }

        var headers = Array.prototype.slice.call(table.tHead.rows[0].cells);
        if (headers.length < 2) {
            return;
        }

        var widths = measureHeaders(table);
        // Ausgeblendete Tabelle (Mobile-Ansicht): später erneut versuchen.
        if (!widths.length || widths.some(function (width) { return width < 1; })) {
            return;
        }

        var colgroup = document.createElement('colgroup');
        widths.forEach(function (width) {
            var col = document.createElement('col');
            col.style.width = width + 'px';
            colgroup.appendChild(col);
        });
        table.insertBefore(colgroup, table.firstChild);

        table.classList.add('column-resize-enabled');
        table.style.width = widths.reduce(function (a, b) { return a + b; }, 0) + 'px';
        table.dataset.columnResizeReady = 'true';

        headers.forEach(function (th, index) {
            // Die letzte Spalte bekommt keinen Griff, ebenso die Auswahlspalte.
            if (index === headers.length - 1) {
                return;
            }
            if (th.querySelector('.form-check')) {
                return;
            }
            if (th.hasAttribute('data-no-column-resize')) {
                return;
            }

            var handle = document.createElement('span');
            handle.className = 'col-resize-handle';
            handle.setAttribute('aria-hidden', 'true');

            // Der Header sortiert per onclick; der Griff darf das nicht auslösen.
            handle.addEventListener('click', function (event) {
                event.preventDefault();
                event.stopPropagation();
            });
            handle.addEventListener('dblclick', function (event) {
                event.preventDefault();
                event.stopPropagation();
            });
            handle.addEventListener('pointerdown', function (event) {
                startResize(event, table, index);
            });

            th.appendChild(handle);
        });
    }

    function initAll() {
        Array.prototype.forEach.call(
            document.querySelectorAll('table[data-resizable-columns]'),
            initTable
        );
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initAll);
    } else {
        initAll();
    }

    // Die E-Mail-Tabelle ist unterhalb von lg ausgeblendet und lässt sich dort
    // nicht messen, beim nächsten Resize erneut versuchen.
    window.addEventListener('resize', initAll);
})();

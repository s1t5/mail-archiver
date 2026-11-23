// Spaltenbreiten in Tabellen per Drag anpassen.
// Die Breiten leben nur im DOM und werden bewusst nicht gespeichert:
// Nach einem Neuladen steht die Tabelle wieder in ihrer natürlichen Breite.
// Tabellen mit data-fit-columns werden zusätzlich auf die Breite ihres
// Scroll-Containers verteilt, damit die E-Mail-Liste ohne horizontales
// Scrollen auskommt. Die flexiblen Spalten tragen dafür data-col-flex mit
// einem Gewicht, alle übrigen behalten ihre Inhaltsbreite.
(function () {
    'use strict';

    var MIN_COLUMN_WIDTH = 48;

    function sum(values) {
        return values.reduce(function (a, b) { return a + b; }, 0);
    }

    function measureHeaders(table, nowrap) {
        // Ohne nowrap misst der Browser umbrochene Zellen, ein Datum etwa nur
        // bis zum Leerzeichen. Die feste Spaltenbreite schneidet den Rest
        // danach ab.
        var previous = table.style.whiteSpace;
        if (nowrap) {
            table.style.whiteSpace = 'nowrap';
        }
        var widths = Array.prototype.map.call(table.tHead.rows[0].cells, function (cell) {
            return cell.getBoundingClientRect().width;
        });
        if (nowrap) {
            table.style.whiteSpace = previous;
        }
        return widths;
    }

    // Verteilt die verfügbare Breite auf die flexiblen Spalten, sobald die
    // natürlichen Breiten den Container sprengen. Gibt null zurück, wenn die
    // Tabelle unverändert bleiben soll.
    function fitWidths(table, widths, available) {
        if (table.dataset.fitColumns !== 'true' || available <= 0) {
            return null;
        }
        if (sum(widths) <= available) {
            return null;
        }

        var headers = table.tHead.rows[0].cells;
        var flexColumns = [];
        var fixedWidth = 0;
        var weightSum = 0;

        widths.forEach(function (width, index) {
            var weight = parseFloat(headers[index].getAttribute('data-col-flex') || '0');
            if (weight > 0) {
                flexColumns.push({ index: index, weight: weight });
                weightSum += weight;
            } else {
                fixedWidth += width;
            }
        });

        if (!flexColumns.length) {
            return null;
        }

        var remaining = available - fixedWidth;
        if (remaining < flexColumns.length * MIN_COLUMN_WIDTH) {
            // Selbst für die Mindestbreiten zu schmal: dann lieber scrollen.
            return null;
        }

        var result = widths.slice();
        var assigned = 0;
        flexColumns.forEach(function (column, position) {
            // Die letzte Spalte bekommt den Rundungsrest, damit die Summe
            // exakt der Containerbreite entspricht.
            var width = position === flexColumns.length - 1
                ? remaining - assigned
                : Math.floor(remaining * column.weight / weightSum);
            result[column.index] = width;
            assigned += width;
        });

        return result;
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

        // Eine gezogene Breite gehört dem Benutzer und wird beim nächsten
        // Resize nicht wieder auf die Containerbreite zurückgesetzt.
        table.dataset.columnResizeUserAdjusted = 'true';

        // Aktuell gerenderte Breiten übernehmen, damit ein vorheriges Strecken
        // der Tabelle (min-width: 100%) den Drag nicht verfälscht.
        var actual = measureHeaders(table);
        var total = sum(actual);
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

    function resetTable(table) {
        Array.prototype.forEach.call(table.querySelectorAll('.col-resize-handle'), function (handle) {
            handle.parentNode.removeChild(handle);
        });

        var colgroup = table.querySelector('colgroup');
        if (colgroup) {
            colgroup.parentNode.removeChild(colgroup);
        }

        table.classList.remove('column-resize-enabled');
        table.style.width = '';
        delete table.dataset.columnResizeReady;
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

        var wrap = table.parentElement;
        var available = wrap ? wrap.clientWidth : 0;
        var widths = measureHeaders(table, table.dataset.fitColumns === 'true');
        // Ausgeblendete Tabelle (Mobile-Ansicht): später erneut versuchen.
        if (!widths.length || widths.some(function (width) { return width < 1; })) {
            return;
        }

        var fitted = fitWidths(table, widths, available);
        if (fitted) {
            widths = fitted;
        }

        var colgroup = document.createElement('colgroup');
        widths.forEach(function (width) {
            var col = document.createElement('col');
            col.style.width = width + 'px';
            colgroup.appendChild(col);
        });
        table.insertBefore(colgroup, table.firstChild);

        table.classList.add('column-resize-enabled');
        table.style.width = sum(widths) + 'px';
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

    var resizeTimer = null;

    function handleResize() {
        if (resizeTimer) {
            clearTimeout(resizeTimer);
        }
        resizeTimer = setTimeout(function () {
            resizeTimer = null;

            // Passt die E-Mail-Tabelle wieder in den Container. Eine vom
            // Benutzer gezogene Breite bleibt unangetastet.
            Array.prototype.forEach.call(
                document.querySelectorAll('table[data-resizable-columns][data-fit-columns="true"]'),
                function (table) {
                    if (table.dataset.columnResizeReady !== 'true') {
                        return;
                    }
                    if (table.dataset.columnResizeUserAdjusted === 'true') {
                        return;
                    }
                    resetTable(table);
                }
            );

            initAll();
        }, 150);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initAll);
    } else {
        initAll();
    }

    // Die E-Mail-Tabelle ist unterhalb von lg ausgeblendet und lässt sich dort
    // nicht messen, beim nächsten Resize erneut versuchen.
    window.addEventListener('resize', handleResize);
})();

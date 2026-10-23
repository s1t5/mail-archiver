// Mailcow mailbox selection: search, select-all and clear, mirroring the M365
// tenant management view (Views/MailAccounts/TenantManagement.cshtml).
document.addEventListener('DOMContentLoaded', function () {
    var rows = Array.from(document.querySelectorAll('.mailcow-mailbox-row'));
    if (rows.length === 0) return;

    var noMatchHint = document.getElementById('noMailcowMailboxMatchHint');
    var searchInput = document.getElementById('mailcowSearch');
    var selectAllBtn = document.getElementById('mailcowSelectAll');
    var clearBtn = document.getElementById('mailcowClear');

    function getVisibleSelectableCheckboxes() {
        return rows
            .filter(function (row) { return row.style.display !== 'none'; })
            .map(function (row) { return row.querySelector('.mailcow-mailbox-checkbox'); })
            .filter(function (cb) { return cb && !cb.disabled; });
    }

    var pendingFrame = null;
    function runFilter() {
        pendingFrame = null;
        var term = (searchInput.value || '').trim().toLowerCase();
        var anyVisible = false;
        rows.forEach(function (row) {
            if (!term) {
                row.style.display = '';
                anyVisible = true;
                return;
            }
            var address = (row.getAttribute('data-address') || '').toLowerCase();
            var name = (row.getAttribute('data-name') || '').toLowerCase();
            if (address.indexOf(term) !== -1 || name.indexOf(term) !== -1) {
                row.style.display = '';
                anyVisible = true;
            } else {
                row.style.display = 'none';
            }
        });
        if (noMatchHint) noMatchHint.style.display = anyVisible ? 'none' : '';
    }

    if (searchInput) {
        searchInput.addEventListener('input', function () {
            if (pendingFrame) { cancelAnimationFrame(pendingFrame); }
            pendingFrame = requestAnimationFrame(runFilter);
        });
    }

    if (selectAllBtn) {
        selectAllBtn.addEventListener('click', function () {
            getVisibleSelectableCheckboxes().forEach(function (cb) { cb.checked = true; });
        });
    }

    if (clearBtn) {
        clearBtn.addEventListener('click', function () {
            getVisibleSelectableCheckboxes().forEach(function (cb) { cb.checked = false; });
        });
    }
});

// Audit data export page: polls active export jobs and updates status, progress
// and action buttons without a full page reload.
(function () {
    'use strict';

    var pollTimer = null;

    function formatBytes(bytes) {
        if (!bytes || bytes <= 0) return '-';
        var sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
        var i = Math.floor(Math.log(bytes) / Math.log(1024));
        return parseFloat((bytes / Math.pow(1024, i)).toFixed(2)) + ' ' + sizes[i];
    }

    function escapeHtml(value) {
        if (value === null || value === undefined) return '';
        return String(value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function findRow(jobId) {
        return document.querySelector('#auditExportJobs tr[data-job-id="' + jobId + '"]');
    }

    function renderActions(job) {
        var actions = '';
        if (job.status === 'Completed' || job.status === 'Downloaded') {
            actions += '<a href="/Logs/DownloadAuditExport?jobId=' + encodeURIComponent(job.jobId) +
                '" class="btn btn-outline-primary btn-sm" title="Download"><i class="bi bi-download"></i></a> ';
        }
        if (job.status === 'Queued' || job.status === 'Running') {
            var token = document.querySelector('#auditExportJobs input[name="__RequestVerificationToken"]');
            actions += '<form method="post" action="/Logs/CancelAuditExport" class="d-inline">' +
                '<input type="hidden" name="__RequestVerificationToken" value="' + (token ? token.value : '') + '" />' +
                '<input type="hidden" name="jobId" value="' + escapeHtml(job.jobId) + '" />' +
                '<button type="submit" class="btn btn-outline-danger btn-sm" title="Abbrechen" ' +
                'onclick="return confirm(\'Soll dieser Export wirklich abgebrochen werden?\');">' +
                '<i class="bi bi-x-circle"></i></button></form>';
        }
        return actions;
    }

    function updateRow(job) {
        var row = findRow(job.jobId);
        if (!row) return;

        row.setAttribute('data-job-status', job.status);

        var progressCell = row.querySelector('.audit-progress');
        if (progressCell) {
            progressCell.textContent = job.processedEmails + ' / ' + job.totalEmails;
        }

        var badge = row.querySelector('.audit-status-badge');
        if (badge) {
            var badgeClass = ({
                'Queued': 'bg-secondary',
                'Running': 'bg-primary',
                'Completed': 'bg-success',
                'Downloaded': 'bg-success',
                'Failed': 'bg-danger',
                'Cancelled': 'bg-warning text-dark'
            })[job.status] || 'bg-secondary';
            var labels = window.auditExportStatusLabels || {};
            badge.className = 'badge ' + badgeClass + ' audit-status-badge';
            badge.textContent = labels[job.status] || job.status;
        }

        // Progress bar: create while running, remove otherwise
        var progressBarContainer = row.querySelector('.progress');
        if (job.status === 'Running') {
            var percent = job.totalEmails > 0 ? Math.round(job.processedEmails * 100 / job.totalEmails) : 0;
            if (!progressBarContainer && badge) {
                badge.insertAdjacentHTML('afterend',
                    '<div class="progress mt-1" style="height: 5px;">' +
                    '<div class="progress-bar progress-bar-striped progress-bar-animated audit-progress-bar" role="progressbar" style="width: 0%"></div>' +
                    '</div>');
            }
            var bar = row.querySelector('.audit-progress-bar');
            if (bar) bar.style.width = percent + '%';
        } else if (progressBarContainer) {
            progressBarContainer.remove();
        }

        var fileSizeCell = row.querySelector('.audit-filesize');
        if (fileSizeCell && job.outputFileSize > 0) {
            fileSizeCell.textContent = formatBytes(job.outputFileSize);
        }

        var actionsCell = row.querySelector('.audit-actions');
        if (actionsCell) {
            actionsCell.innerHTML = renderActions(job);
        }
    }

    function poll() {
        var rows = document.querySelectorAll('#auditExportJobs tr[data-job-id]');
        var activeJobIds = [];
        rows.forEach(function (row) {
            var status = row.getAttribute('data-job-status');
            if (status === 'Queued' || status === 'Running') {
                activeJobIds.push(row.getAttribute('data-job-id'));
            }
        });

        if (activeJobIds.length === 0) {
            if (pollTimer) {
                clearInterval(pollTimer);
                pollTimer = null;
            }
            return;
        }

        activeJobIds.forEach(function (jobId) {
            fetch('/Logs/AuditExportStatus?jobId=' + encodeURIComponent(jobId), {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            })
                .then(function (response) { return response.ok ? response.json() : null; })
                .then(function (job) { if (job) updateRow(job); })
                .catch(function () { /* transient network error, retry on next tick */ });
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        var container = document.getElementById('auditExportJobs');
        if (!container) return;

        poll();
        if (container.querySelector('tr[data-job-status="Queued"], tr[data-job-status="Running"]')) {
            pollTimer = setInterval(poll, 3000);
        }
    });
})();
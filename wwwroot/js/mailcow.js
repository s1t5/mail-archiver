(() => {
    const rows = [...document.querySelectorAll('[data-mailcow-mailbox]')];
    const search = document.getElementById('mailcowSearch');
    search?.addEventListener('input', () => {
        const query = search.value.toLocaleLowerCase();
        rows.forEach(row => { row.hidden = !row.textContent.toLocaleLowerCase().includes(query); });
    });
    document.getElementById('mailcowSelectAll')?.addEventListener('click', () => {
        rows.filter(row => !row.hidden).forEach(row => {
            const box = row.querySelector('input[type="checkbox"]');
            if (box && !box.disabled) box.checked = true;
        });
    });
    document.getElementById('mailcowClear')?.addEventListener('click', () => {
        rows.forEach(row => { row.querySelector('input[type="checkbox"]').checked = false; });
    });
})();

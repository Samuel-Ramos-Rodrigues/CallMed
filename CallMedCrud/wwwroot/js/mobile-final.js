(() => {
    const root = document.documentElement;
    const body = document.body;
    const viewport = window.visualViewport;

    let baselineHeight = Math.max(window.innerHeight, viewport?.height || 0);

    const updateViewport = () => {
        const vvHeight = Math.round(viewport?.height || window.innerHeight);
        const vvWidth = Math.round(viewport?.width || window.innerWidth);
        const orientationChanged = vvWidth > vvHeight && baselineHeight < vvWidth;

        if (!orientationChanged && vvHeight > baselineHeight - 80) {
            baselineHeight = Math.max(baselineHeight, vvHeight);
        }

        root.style.setProperty('--cm-vv-height', `${vvHeight}px`);
        root.style.setProperty('--cm-vv-offset-top', `${Math.round(viewport?.offsetTop || 0)}px`);

        const active = document.activeElement;
        const editable = active && (
            active.matches?.('input:not([type="checkbox"]):not([type="radio"]):not([type="button"]):not([type="submit"]), textarea, select, [contenteditable="true"]')
        );
        const keyboardByViewport = baselineHeight - vvHeight > 140;
        body?.classList.toggle('cm-keyboard-open', Boolean(editable && keyboardByViewport));
    };

    const clearKeyboardStateSoon = () => {
        window.setTimeout(updateViewport, 80);
        window.setTimeout(updateViewport, 260);
    };

    viewport?.addEventListener('resize', updateViewport, { passive: true });
    viewport?.addEventListener('scroll', updateViewport, { passive: true });
    window.addEventListener('resize', updateViewport, { passive: true });
    window.addEventListener('orientationchange', () => {
        baselineHeight = 0;
        window.setTimeout(() => {
            baselineHeight = Math.max(window.innerHeight, viewport?.height || 0);
            updateViewport();
        }, 250);
    }, { passive: true });
    document.addEventListener('focusin', clearKeyboardStateSoon);
    document.addEventListener('focusout', clearKeyboardStateSoon);

    updateViewport();
})();

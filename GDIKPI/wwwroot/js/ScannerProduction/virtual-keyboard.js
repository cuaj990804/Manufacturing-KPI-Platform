(function () {
    const hasExplicitSelector = Boolean(window.SCANNER_KEYBOARD_SELECTOR);
    const isTouchDevice = window.matchMedia('(pointer: coarse)').matches || (navigator.maxTouchPoints || 0) > 0;
    const keyboardGlobal = window.SimpleKeyboard && (window.SimpleKeyboard.default || window.SimpleKeyboard);

    if ((!isTouchDevice && !hasExplicitSelector) || !keyboardGlobal) {
        return;
    }

    const SUPPORTED_SELECTOR = window.SCANNER_KEYBOARD_SELECTOR || '#rejectDefectSearch';

    const keyboardState = {
        instance: null,
        activeInput: null,
        layoutName: 'default',
        host: null,
        container: null
    };

    function ensureKeyboardHost() {
        if (keyboardState.host && keyboardState.container) {
            return;
        }

        let host = document.getElementById('scannerVirtualKeyboardHost');
        if (!host) {
            host = document.createElement('div');
            host.id = 'scannerVirtualKeyboardHost';
            host.className = 'scanner-virtual-keyboard hidden';
            host.innerHTML = '<div id="scannerVirtualKeyboard" class="simple-keyboard"></div>';
            document.body.appendChild(host);
        }

        host.addEventListener('mousedown', (event) => event.preventDefault());
        host.addEventListener('touchstart', (event) => event.stopPropagation(), { passive: true });
        host.addEventListener('click', (event) => event.stopPropagation());

        keyboardState.host = host;
        keyboardState.container = host.querySelector('#scannerVirtualKeyboard');
    }

    function createKeyboardInstance() {
        ensureKeyboardHost();
        if (keyboardState.instance || !keyboardState.container) {
            return;
        }

        keyboardState.instance = new keyboardGlobal(keyboardState.container, {
            layoutName: keyboardState.layoutName,
            theme: 'hg-theme-default scanner-ios-theme',
            physicalKeyboardHighlight: false,
            syncInstanceInputs: false,
            mergeDisplay: true,
            display: {
                '{bksp}': 'Bksp',
                '{enter}': 'Aceptar',
                '{shift}': 'Shift',
                '{space}': 'espacio',
                '{numbers}': '123',
                '{abc}': 'ABC',
                '{hide}': 'Ocultar'
            },
            buttonTheme: [
                { class: 'hg-functionBtn', buttons: '{shift} {numbers} {abc} {space} {hide}' }
            ],
            layout: {
                default: [
                    '1 2 3 4 5 6 7 8 9 0',
                    'q w e r t y u i o p',
                    'a s d f g h j k l',
                    '{shift} z x c v b n m {bksp}',
                    '{numbers} {space} {enter} {hide}'
                ],
                shift: [
                    '1 2 3 4 5 6 7 8 9 0',
                    'Q W E R T Y U I O P',
                    'A S D F G H J K L',
                    '{shift} Z X C V B N M {bksp}',
                    '{numbers} {space} {enter} {hide}'
                ],
                numbers: [
                    '1 2 3 4 5 6 7 8 9 0',
                    '- / : ; ( ) $ & @ "',
                    '. , ? ! \' # %',
                    '{abc} {space} {bksp}',
                    '{enter} {hide}'
                ]
            },
            onKeyPress: handleVirtualKeyPress,
            onChange: handleVirtualKeyboardChange
        });
    }

    function handleVirtualKeyboardChange(inputValue) {
        if (!keyboardState.activeInput) {
            return;
        }

        keyboardState.activeInput.value = inputValue;
        keyboardState.activeInput.dispatchEvent(new Event('input', { bubbles: true }));
    }

    function handleVirtualKeyPress(button) {
        if (!keyboardState.activeInput) {
            return;
        }

        if (button === '{shift}') {
            const nextLayout = keyboardState.layoutName === 'default' ? 'shift' : 'default';
            setKeyboardLayout(nextLayout);
            return;
        }

        if (button === '{numbers}') {
            setKeyboardLayout('numbers');
            return;
        }

        if (button === '{abc}') {
            setKeyboardLayout('default');
            return;
        }

        if (button === '{enter}') {
            triggerEnter(keyboardState.activeInput);
            return;
        }

        if (button === '{hide}') {
            const activeInput = keyboardState.activeInput;
            hideKeyboard();
            if (activeInput) {
                activeInput.blur();
            }
            return;
        }

        if (keyboardState.layoutName === 'shift' && button.length === 1) {
            setKeyboardLayout('default');
        }
    }

    function setKeyboardLayout(layoutName) {
        keyboardState.layoutName = layoutName;
        if (keyboardState.instance) {
            keyboardState.instance.setOptions({ layoutName });
        }
    }

    function triggerEnter(target) {
        ['keydown', 'keypress', 'keyup'].forEach((eventName) => {
            target.dispatchEvent(new KeyboardEvent(eventName, {
                key: 'Enter',
                code: 'Enter',
                bubbles: true
            }));
        });
    }

    function showKeyboard(target) {
        if (!target || target.disabled || target.readOnly) {
            return;
        }

        createKeyboardInstance();

        keyboardState.activeInput = target;
        target.setAttribute('inputmode', 'none');
        target.setAttribute('autocomplete', 'off');
        target.setAttribute('autocorrect', 'off');
        target.setAttribute('autocapitalize', 'off');
        target.setAttribute('spellcheck', 'false');

        if (keyboardState.instance) {
            keyboardState.instance.setInput(target.value || '');
            if (keyboardState.layoutName === 'numbers') {
                setKeyboardLayout('default');
            }
        }

        keyboardState.host.classList.remove('hidden');
        document.body.classList.add('scanner-keyboard-open');
    }

    function hideKeyboard() {
        if (!keyboardState.host) {
            return;
        }

        keyboardState.host.classList.add('hidden');
        document.body.classList.remove('scanner-keyboard-open');
        keyboardState.activeInput = null;
        setKeyboardLayout('default');
    }

    function isSupportedInput(target) {
        return target instanceof HTMLElement && target.matches(SUPPORTED_SELECTOR);
    }

    function isKeyboardTrigger(target) {
        return target instanceof HTMLElement && Boolean(target.closest('[data-scanner-keyboard-trigger]'));
    }

    document.addEventListener('focusin', (event) => {
        const target = event.target;
        if (isSupportedInput(target)) {
            showKeyboard(target);
        }
    });

    document.addEventListener('input', (event) => {
        const target = event.target;
        if (!isSupportedInput(target) || keyboardState.activeInput !== target || !keyboardState.instance) {
            return;
        }

        keyboardState.instance.setInput(target.value || '');
    });

    document.addEventListener('click', (event) => {
        if (!keyboardState.host || keyboardState.host.classList.contains('hidden')) {
            return;
        }

        const target = event.target;
        const clickedKeyboard = keyboardState.host.contains(target);
        const clickedInput = isSupportedInput(target);
        const clickedTrigger = isKeyboardTrigger(target);

        if (!clickedKeyboard && !clickedInput && !clickedTrigger) {
            hideKeyboard();
        }
    });

    document.addEventListener('DOMContentLoaded', () => {
        ensureKeyboardHost();

        const activeElement = document.activeElement;
        if (isSupportedInput(activeElement)) {
            showKeyboard(activeElement);
        }
    });

    window.ScannerVirtualKeyboard = {
        showForInput: showKeyboard,
        hide: hideKeyboard
    };
})();

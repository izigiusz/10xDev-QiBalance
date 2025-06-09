// Enhanced diagnostic UX functions
// QiBalance - TCM Diagnostic System

(function () {
    'use strict';

    let diagnosticComponent = null;
    let keyboardListenerActive = false;
    let debounceTimers = new Map();

    // Initialize keyboard shortcuts
    window.setupKeyboardShortcuts = function (dotNetComponent) {
        diagnosticComponent = dotNetComponent;
        
        if (!keyboardListenerActive) {
            document.addEventListener('keydown', handleKeyboardInput, { passive: false });
            keyboardListenerActive = true;
            console.log('[Diagnostic] Keyboard shortcuts initialized');
        }
    };

    // Enhanced keyboard input handler with debouncing
    function handleKeyboardInput(event) {
        if (!diagnosticComponent) return;

        // Ignore if user is typing in input fields
        if (event.target.tagName === 'INPUT' || event.target.tagName === 'TEXTAREA') {
            return;
        }

        const key = event.code;
        const mappedKeys = ['KeyT', 'KeyN', 'Space', 'Enter', 'Escape'];
        
        if (mappedKeys.includes(key)) {
            event.preventDefault();
            
            // Debounce to prevent rapid key presses
            debounceAction(`keyboard-${key}`, () => {
                diagnosticComponent.invokeMethodAsync('HandleKeyboardShortcut', key)
                    .catch(err => console.warn('[Diagnostic] Keyboard shortcut error:', err));
            }, 150);
        }
    }

    // Debounce helper function
    function debounceAction(key, action, delay) {
        if (debounceTimers.has(key)) {
            clearTimeout(debounceTimers.get(key));
        }
        
        debounceTimers.set(key, setTimeout(() => {
            action();
            debounceTimers.delete(key);
        }, delay));
    }

    // Setup smooth scrolling
    window.setupSmoothScrolling = function () {
        // Enable smooth scrolling for all elements
        document.documentElement.style.scrollBehavior = 'smooth';
        console.log('[Diagnostic] Smooth scrolling enabled');
    };

    // Smooth scroll to specific element
    window.smoothScrollToElement = function (className) {
        const element = document.querySelector(`.${className}`);
        if (element) {
            element.scrollIntoView({ 
                behavior: 'smooth', 
                block: 'center',
                inline: 'nearest' 
            });
        }
    };

    // Setup accessibility enhancements
    window.setupAccessibilityEnhancements = function () {
        // Add focus indicators for keyboard navigation
        addFocusIndicators();
        
        // Setup screen reader announcements
        setupAriaLiveRegions();
        
        // Improve contrast for better visibility
        enhanceVisualContrast();
        
        console.log('[Diagnostic] Accessibility enhancements applied');
    };

    function addFocusIndicators() {
        const style = document.createElement('style');
        style.textContent = `
            .btn:focus-visible,
            .card:focus-visible {
                outline: 3px solid #0d6efd;
                outline-offset: 2px;
                border-radius: 4px;
            }
            
            .answer-button:focus-visible {
                transform: scale(1.05);
                box-shadow: 0 4px 8px rgba(0,0,0,0.2);
            }
        `;
        document.head.appendChild(style);
    }

    function setupAriaLiveRegions() {
        // Create live region for dynamic announcements
        if (!document.getElementById('diagnostic-announcements')) {
            const liveRegion = document.createElement('div');
            liveRegion.id = 'diagnostic-announcements';
            liveRegion.setAttribute('aria-live', 'polite');
            liveRegion.setAttribute('aria-atomic', 'true');
            liveRegion.style.position = 'absolute';
            liveRegion.style.left = '-10000px';
            liveRegion.style.width = '1px';
            liveRegion.style.height = '1px';
            liveRegion.style.overflow = 'hidden';
            document.body.appendChild(liveRegion);
        }
    }

    function enhanceVisualContrast() {
        // Add high contrast mode detection
        if (window.matchMedia && window.matchMedia('(prefers-contrast: high)').matches) {
            document.body.classList.add('high-contrast-mode');
        }
    }

    // Haptic feedback for mobile devices
    window.triggerHapticFeedback = function (type = 'light') {
        if ('vibrate' in navigator) {
            const patterns = {
                light: [10],
                medium: [20],
                heavy: [30],
                success: [10, 100, 10],
                error: [100, 50, 100, 50, 100]
            };
            
            navigator.vibrate(patterns[type] || patterns.light);
        }
    };

    // Phase transition animations
    window.showPhaseTransition = function (message) {
        removeExistingTransition();
        
        const overlay = createTransitionOverlay(message);
        document.body.appendChild(overlay);
        
        // Animate in
        requestAnimationFrame(() => {
            overlay.style.opacity = '1';
            overlay.querySelector('.transition-content').style.transform = 'translateY(0) scale(1)';
        });
        
        // Auto-remove after 3 seconds
        setTimeout(() => {
            removeTransitionOverlay(overlay);
        }, 3000);
        
        // Announce to screen readers
        announceToScreenReader(message);
    };

    function createTransitionOverlay(message) {
        const overlay = document.createElement('div');
        overlay.className = 'phase-transition-overlay';
        overlay.style.cssText = `
            position: fixed;
            top: 0;
            left: 0;
            right: 0;
            bottom: 0;
            background: rgba(13, 110, 253, 0.9);
            backdrop-filter: blur(10px);
            z-index: 9999;
            display: flex;
            align-items: center;
            justify-content: center;
            opacity: 0;
            transition: opacity 0.5s ease;
        `;
        
        const content = document.createElement('div');
        content.className = 'transition-content';
        content.style.cssText = `
            background: white;
            padding: 2rem;
            border-radius: 15px;
            text-align: center;
            max-width: 400px;
            box-shadow: 0 20px 40px rgba(0,0,0,0.3);
            transform: translateY(-50px) scale(0.9);
            transition: transform 0.5s ease;
        `;
        
        content.innerHTML = `
            <div class="text-primary mb-3" style="font-size: 3rem;">
                <i class="bi bi-arrow-right-circle"></i>
            </div>
            <h4 class="text-primary mb-2">Następna Faza</h4>
            <p class="text-muted mb-0">${message}</p>
        `;
        
        overlay.appendChild(content);
        return overlay;
    }

    function removeExistingTransition() {
        const existing = document.querySelector('.phase-transition-overlay');
        if (existing) {
            removeTransitionOverlay(existing);
        }
    }

    function removeTransitionOverlay(overlay) {
        overlay.style.opacity = '0';
        setTimeout(() => {
            if (overlay.parentNode) {
                overlay.parentNode.removeChild(overlay);
            }
        }, 500);
    }

    // Completion celebration
    window.showCompletionCelebration = function () {
        // Confetti effect
        if (typeof confetti !== 'undefined') {
            confetti({
                particleCount: 100,
                spread: 70,
                origin: { y: 0.6 }
            });
        }
        
        // Success announcement
        announceToScreenReader('Diagnoza zakończona pomyślnie! Przygotowywanie wyników...');
        
        // Success haptic
        window.triggerHapticFeedback('success');
    };

    // Screen reader announcements
    function announceToScreenReader(message) {
        const liveRegion = document.getElementById('diagnostic-announcements');
        if (liveRegion) {
            liveRegion.textContent = message;
            // Clear after announcement
            setTimeout(() => {
                liveRegion.textContent = '';
            }, 2000);
        }
    }

    // Focus management
    window.getCurrentFocusedElementId = function () {
        const activeElement = document.activeElement;
        return activeElement ? activeElement.id || activeElement.className : '';
    };

    window.restoreFocus = function (elementIdentifier) {
        if (!elementIdentifier) return;
        
        let element = document.getElementById(elementIdentifier);
        if (!element) {
            element = document.querySelector(`.${elementIdentifier}`);
        }
        
        if (element && typeof element.focus === 'function') {
            setTimeout(() => element.focus(), 100);
        }
    };

    // Performance optimizations
    window.optimizePerformance = function () {
        // Intersection Observer for lazy loading
        if ('IntersectionObserver' in window) {
            setupLazyLoading();
        }
        
        // Preload next question images/content
        preloadResources();
        
        // Setup performance monitoring
        setupPerformanceMonitoring();
    };

    function setupLazyLoading() {
        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    const element = entry.target;
                    if (element.dataset.lazySrc) {
                        element.src = element.dataset.lazySrc;
                        element.removeAttribute('data-lazy-src');
                        observer.unobserve(element);
                    }
                }
            });
        });

        document.querySelectorAll('[data-lazy-src]').forEach(img => {
            observer.observe(img);
        });
    }

    function preloadResources() {
        // Preload critical CSS
        const criticalCSS = ['/css/diagnostic-components.css'];
        criticalCSS.forEach(href => {
            const link = document.createElement('link');
            link.rel = 'preload';
            link.as = 'style';
            link.href = href;
            document.head.appendChild(link);
        });
    }

    function setupPerformanceMonitoring() {
        // Monitor page load performance
        if ('performance' in window) {
            window.addEventListener('load', () => {
                setTimeout(() => {
                    const timing = performance.timing;
                    const loadTime = timing.loadEventEnd - timing.navigationStart;
                    console.log(`[Diagnostic] Page load time: ${loadTime}ms`);
                    
                    // Report slow loads
                    if (loadTime > 3000) {
                        console.warn('[Diagnostic] Slow page load detected');
                    }
                }, 0);
            });
        }
    }

    // Network status monitoring
    window.setupNetworkMonitoring = function () {
        if ('navigator' in window && 'onLine' in navigator) {
            function updateOnlineStatus() {
                const isOnline = navigator.onLine;
                const indicator = document.querySelector('.network-status');
                
                if (indicator) {
                    indicator.className = `network-status ${isOnline ? 'online' : 'offline'}`;
                    indicator.textContent = isOnline ? 'Online' : 'Offline';
                }
                
                if (!isOnline) {
                    announceToScreenReader('Połączenie internetowe zostało przerwane');
                }
            }

            window.addEventListener('online', updateOnlineStatus);
            window.addEventListener('offline', updateOnlineStatus);
            updateOnlineStatus();
        }
    };

    // Cleanup function
    window.cleanupDiagnosticEnhancements = function () {
        if (keyboardListenerActive) {
            document.removeEventListener('keydown', handleKeyboardInput);
            keyboardListenerActive = false;
        }
        
        diagnosticComponent = null;
        debounceTimers.clear();
        
        // Remove any active overlays
        removeExistingTransition();
        
        console.log('[Diagnostic] Enhancements cleaned up');
    };

    // Initialize on DOMContentLoaded
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', window.optimizePerformance);
    } else {
        window.optimizePerformance();
    }

})(); 
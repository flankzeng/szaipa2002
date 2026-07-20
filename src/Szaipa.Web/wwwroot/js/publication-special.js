(function () {
    'use strict';

    function initializeFilters() {
        var filterButtons = Array.from(document.querySelectorAll('.filter'));
        var filterBoxes = Array.from(document.querySelectorAll('.filterBox'));

        filterButtons.forEach(function (button) {
            button.addEventListener('click', function () {
                var value = button.dataset.filter;

                filterBoxes.forEach(function (box) {
                    box.classList.toggle('hidden', value !== 'all' && !box.classList.contains(value));
                });

                filterButtons.forEach(function (candidate) {
                    candidate.classList.toggle('bold', candidate === button);
                });
            });
        });
    }

    function initializeTopButton() {
        var topButton = document.querySelector('#topBtn');
        var firstSection = document.querySelector('#section01');

        if (!topButton || !firstSection) {
            return;
        }

        window.addEventListener('scroll', function () {
            var scrollPosition = window.scrollY;
            var withinFirstSection = scrollPosition >= firstSection.offsetTop
                && scrollPosition < firstSection.offsetTop + firstSection.offsetHeight;

            topButton.style.display = withinFirstSection ? 'none' : 'block';
        }, { passive: true });

        topButton.addEventListener('click', function () {
            window.scrollTo({
                top: 0,
                behavior: 'smooth',
            });
        });
    }

    function initializeNavigation() {
        var navigationItems = Array.from(document.querySelectorAll('.nav-item'));

        navigationItems.forEach(function (item) {
            item.addEventListener('click', function () {
                navigationItems.forEach(function (candidate) {
                    candidate.classList.remove('active');
                });
                item.classList.add('active');
            });
        });
    }

    function initializeCatalog() {
        var catalog = document.querySelector('.mySwiper');
        if (!catalog || typeof Swiper !== 'function') {
            return;
        }

        new Swiper(catalog, {
            effect: 'coverflow',
            initialSlide: Number(document.body.dataset.initialSlide || 0),
            grabCursor: true,
            centeredSlides: true,
            slidesPerView: 'auto',
            loop: true,
            lazy: true,
            coverflowEffect: {
                rotate: 20,
                stretch: 0,
                depth: 100,
                modifier: 1,
                slideShadows: true,
            },
            navigation: {
                nextEl: catalog.querySelector('.swiper-button-next'),
                prevEl: catalog.querySelector('.swiper-button-prev'),
            },
            pagination: {
                el: catalog.querySelector('.swiper-pagination'),
                clickable: true,
            },
            keyboard: {
                enabled: true,
                onlyInViewport: true,
            },
            on: {
                click: function () {
                    this.slideTo(this.clickedIndex);
                },
            },
        });
    }

    function initializeWorkLinks() {
        document.querySelectorAll('.link').forEach(function (link) {
            link.addEventListener('click', function (event) {
                event.preventDefault();

                var target = document.querySelector(link.getAttribute('href'));
                if (!target) {
                    return;
                }

                document.querySelectorAll('.target').forEach(function (candidate) {
                    candidate.classList.remove('displayInfo');
                });

                target.classList.add('displayInfo');
                target.scrollIntoView({ behavior: 'smooth' });
            });
        });
    }

    function createOriginalImageRestorer(mainGallery) {
        var existingRestorer = mainGallery.szaipaOriginalImageRestorer;
        if (existingRestorer) {
            return existingRestorer;
        }

        var originalImageLoads = new Map();
        var gallerySwiper = null;
        var observer = null;
        var restorationEnabled = false;

        function preloadOriginal(url) {
            if (originalImageLoads.has(url)) {
                return originalImageLoads.get(url);
            }

            var load = new Promise(function (resolve) {
                var preloader = new Image();

                preloader.onload = function () {
                    resolve(true);
                };
                preloader.onerror = function () {
                    resolve(false);
                };
                preloader.src = url;
            });

            originalImageLoads.set(url, load);
            return load;
        }

        function restoreImage(image) {
            var originalUrl = image.getAttribute('data-original-src');

            if (!originalUrl
                || image.dataset.originalRestored === 'true'
                || image.getAttribute('src') === originalUrl) {
                image.dataset.originalRestored = 'true';
                return;
            }

            preloadOriginal(originalUrl).then(function (loaded) {
                if (!loaded
                    || !document.documentElement.contains(image)
                    || image.getAttribute('data-original-src') !== originalUrl
                    || image.dataset.originalRestored === 'true') {
                    return;
                }

                image.setAttribute('src', originalUrl);
                image.dataset.originalRestored = 'true';
            });
        }

        function restoreAround(swiper) {
            if (!restorationEnabled || !swiper || !swiper.slides || swiper.slides.length === 0) {
                return;
            }

            var adjacentSlides = Array.from(mainGallery.querySelectorAll(
                '.swiper-slide-active, .swiper-slide-prev, .swiper-slide-next'
            ));

            var slideCount = swiper.slides.length;
            [-1, 0, 1].forEach(function (offset) {
                var slideIndex = (swiper.activeIndex + offset + slideCount) % slideCount;
                var slide = swiper.slides[slideIndex];

                if (slide && !adjacentSlides.includes(slide)) {
                    adjacentSlides.push(slide);
                }
            });

            adjacentSlides.forEach(function (slide) {
                slide.querySelectorAll('img[data-original-src]').forEach(restoreImage);
            });
        }

        function enableRestoration() {
            if (restorationEnabled) {
                return;
            }

            restorationEnabled = true;

            if (observer) {
                observer.disconnect();
                observer = null;
            }

            restoreAround(gallerySwiper);
        }

        function connect(swiper) {
            gallerySwiper = swiper;

            if (restorationEnabled) {
                restoreAround(swiper);
                return;
            }

            if (observer) {
                return;
            }

            if (typeof window.IntersectionObserver !== 'function') {
                enableRestoration();
                return;
            }

            observer = new window.IntersectionObserver(function (entries) {
                if (entries.some(function (entry) {
                    return entry.isIntersecting || entry.intersectionRatio > 0;
                })) {
                    enableRestoration();
                }
            }, {
                rootMargin: '50% 0%',
            });
            observer.observe(mainGallery);
        }

        var restorer = {
            connect: connect,
            restoreAround: restoreAround,
        };

        mainGallery.szaipaOriginalImageRestorer = restorer;
        return restorer;
    }

    function initializeGallery() {
        var thumbnailGallery = document.querySelector('.mySwiper2');
        var mainGallery = document.querySelector('.mySwiper3');

        if (!thumbnailGallery || !mainGallery || typeof Swiper !== 'function') {
            return;
        }

        if (thumbnailGallery.swiper
            || mainGallery.swiper
            || thumbnailGallery.dataset.szaipaGalleryInitialized === 'true'
            || mainGallery.dataset.szaipaGalleryInitialized === 'true') {
            return;
        }

        thumbnailGallery.dataset.szaipaGalleryInitialized = 'true';
        mainGallery.dataset.szaipaGalleryInitialized = 'true';

        var thumbnailSwiper = new Swiper(thumbnailGallery, {
            loop: true,
            grabCursor: true,
            spaceBetween: 3,
            slidesPerView: 7,
            freeMode: true,
            watchSlidesProgress: true,
        });

        var originalImageRestorer = createOriginalImageRestorer(mainGallery);

        new Swiper(mainGallery, {
            loop: true,
            spaceBetween: 0,
            grabCursor: true,
            lazy: true,
            navigation: {
                nextEl: mainGallery.querySelector('.swiper-button-next'),
                prevEl: mainGallery.querySelector('.swiper-button-prev'),
            },
            thumbs: {
                swiper: thumbnailSwiper,
            },
            keyboard: {
                enabled: true,
                onlyInViewport: true,
            },
            on: {
                init: function () {
                    originalImageRestorer.connect(this);
                },
                slideChangeTransitionStart: function () {
                    originalImageRestorer.restoreAround(this);
                },
            },
        });
    }

    function initializePage() {
        initializeFilters();
        initializeTopButton();
        initializeNavigation();
        initializeCatalog();
        initializeWorkLinks();
        initializeGallery();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializePage, { once: true });
    } else {
        initializePage();
    }
})();

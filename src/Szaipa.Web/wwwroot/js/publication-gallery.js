// Dual-swiper exhibition gallery (main image swiper + thumbnail swiper), shared by every
// exhibition/publication page. Swiper has no jQuery dependency, so keep this initializer native.
(function () {
    function createOriginalImageRestorer(mainGallery) {
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

        return {
            connect: connect,
            restoreAround: restoreAround,
        };
    }

    function initializeGallery() {
        var thumbnailGallery = document.querySelector('.mySwiper2');
        var mainGallery = document.querySelector('.mySwiper3');

        if (!thumbnailGallery || !mainGallery || typeof Swiper !== 'function') {
            return;
        }

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
                nextEl: '.swiper-button-next',
                prevEl: '.swiper-button-prev',
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

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializeGallery, { once: true });
    } else {
        initializeGallery();
    }
})();

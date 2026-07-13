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
                nextEl: '.swiper-button-next',
                prevEl: '.swiper-button-prev',
            },
            pagination: {
                el: '.swiper-pagination',
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

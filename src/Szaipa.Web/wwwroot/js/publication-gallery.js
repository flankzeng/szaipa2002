// Dual-swiper exhibition gallery (main image swiper + thumbnail swiper), shared by every
// exhibition/publication page. Extracted from the per-page inline scripts that were byte-identical.
$(function () {
    var swiper2 = new Swiper(".mySwiper2", {
        loop: true,
        grabCursor: true,
        spaceBetween: 3,
        slidesPerView: 7,
        freeMode: true,
        watchSlidesProgress: true,
    });

    new Swiper(".mySwiper3", {
        loop: true,
        spaceBetween: 0,
        grabCursor: true,
        lazy: true,
        navigation: {
            nextEl: ".swiper-button-next",
            prevEl: ".swiper-button-prev",
        },
        thumbs: {
            swiper: swiper2,
        },
        keyboard: {
            enabled: true,
            onlyInViewport: true,
        },
    });
});

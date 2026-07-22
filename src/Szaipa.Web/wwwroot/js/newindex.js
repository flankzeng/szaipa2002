    // 第一个轮播在手机端由 xs-hide 隐藏；隐藏状态初始化 Swiper 会抛错并中断整页脚本。
    var heroSwiperElement = document.querySelector('.mySwiper');
    if (heroSwiperElement && heroSwiperElement.offsetParent !== null) {
        var swiper = new Swiper(heroSwiperElement, {
            slidesPerView: "auto",
            centeredSlides: true,
            spaceBetween: 0,
            loop: true,
            autoplay: {
                delay: 5000,
                disableOnInteraction: false,
            },
            pagination: {
                el: ".swiper-pagination",
                clickable: true,
            },
            keyboard: {
                enabled: true,
                onlyInViewport: true,
            },
        });

        heroSwiperElement.addEventListener('mouseenter', function () {
            swiper.autoplay.stop();
        });
        heroSwiperElement.addEventListener('mouseleave', function () {
            swiper.autoplay.start();
        });
    }

    // topFunction
    const toTopButton = document.querySelector('#topBtn');
    const section04 = document.querySelector('#section04');
    const section08 = document.querySelector('#section08');

    section04.addEventListener('mouseover', function () {
        toTopButton.style.display = 'block';
    });

    section08.addEventListener('mouseover', function () {
        toTopButton.style.display = 'block';
    });

    window.addEventListener('scroll', function () {
        if (window.pageYOffset === 0) {
            toTopButton.style.display = 'none';
        }
    }, { passive: true });

    toTopButton.addEventListener('click', function () {
        window.scrollTo(0, 0);
    });

    // 新增样式不可抓取
    document.querySelectorAll('.undraggable').forEach(function (element) {
        element.addEventListener('dragstart', function (event) {
            event.preventDefault();
        });
    });

    // 关于在展活动的Swiper
    var swiper10 = new Swiper(".mySwiper10", {
        pagination: {
            el: ".swiper-pagination",
            type: "progressbar",
        },
        keyboard: {
            enabled: true,
            onlyInViewport: true,
        },
        loop: true,
        autoplay: {
            delay: 5000,
        },
        navigation: {
            nextEl: '.swiper-button-next',
            prevEl: '.swiper-button-prev',
        },
        disableOnInteraction: true
    })

    // 放大镜：首次交互时再请求原图，列表首屏只下载缩略图。
    window.SzaipaMagnifyLoader?.bind(document);

    // 展会轮播
    document.querySelectorAll('.processing-lunbo').forEach(function (carousel) {
        var slides = carousel.querySelectorAll('.exhibitionSlide');
        var currentSlide = 0;
        if (slides.length === 0) {
            return;
        }

        function showSlide() {
            slides[currentSlide].classList.remove('borderActive');
            currentSlide = (currentSlide + 1) % slides.length;
            slides[currentSlide].classList.add('borderActive');
        }

        setInterval(showSlide, 2300);
    });

    // 展会活动激活状态
    var btn01 = document.querySelector('#btn01');
    var btn02 = document.querySelector('#btn02');
    var processing = document.querySelector('#processing');
    var ended = document.querySelector('#ended');
    btn01?.classList.add('active');

    btn01?.addEventListener('click', function () {
        btn02?.classList.remove('active');
        this.classList.add('active');
        if (processing) {
            processing.style.display = 'block';
        }
        if (ended) {
            ended.style.display = 'none';
        }
    });

    btn02?.addEventListener('click', function () {
        btn01?.classList.remove('active');
        this.classList.add('active');
        if (processing) {
            processing.style.display = 'none';
        }
        if (ended) {
            ended.style.display = 'flex';
            ended.style.flexWrap = 'wrap';
        }
    });

    // 关于在售作品的Swiper
    var swiper6 = new Swiper(".mySwiper6", {
        pagination: {
            el: ".swiper-pagination",
            type: "progressbar",
        },
        keyboard: {
            enabled: true,
            onlyInViewport: true,
        },
        loop: true,
        // autoplay: false,
        autoplay: {
            delay: 5000,
            disableOnInteraction: false,
        },
        navigation: {
            nextEl: '.swiper-button-next',
            prevEl: '.swiper-button-prev',
        },
        disableOnInteraction: true
    })

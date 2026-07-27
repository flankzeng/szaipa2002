    // 成员卡在手机端按每行三列排版。最后一行不足三人时补不可见单元格，
    // 以免顾问团队等短列表改变三列节奏；成员增减后无需再手工维护占位元素。
    document.querySelectorAll('.member').forEach(function (member) {
        member.querySelectorAll('.member-placeholder').forEach(function (placeholder) {
            placeholder.remove();
        });

        var memberCount = member.querySelectorAll('.member-item').length;
        var placeholdersNeeded = (3 - (memberCount % 3)) % 3;
        for (var placeholderIndex = 0; placeholderIndex < placeholdersNeeded; placeholderIndex += 1) {
            var placeholder = document.createElement('span');
            placeholder.className = 'member-placeholder';
            placeholder.setAttribute('aria-hidden', 'true');
            member.appendChild(placeholder);
        }
    });

    // Section02 现在也在手机端显示，因此始终初始化首页轮播。
    var heroSwiperElement = document.querySelector('.mySwiper');
    if (heroSwiperElement && heroSwiperElement.offsetParent !== null) {
        var swiper = new Swiper(heroSwiperElement, {
            // 首页大图始终一屏一张。auto 在 2K/4K 视口会按旧的 100vw slide
            // 宽度参与计算，造成右侧留出未覆盖的空白。
            slidesPerView: 1,
            centeredSlides: false,
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

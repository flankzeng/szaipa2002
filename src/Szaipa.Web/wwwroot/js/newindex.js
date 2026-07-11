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
    $(function () {
        $(".undraggable").on("dragstart", function (event) {
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

    $('.next-button').on('click', function () {
        swiper10.slideNext();
    });

    $('.prev-button').on('click', function () {
        swiper10.slidePrev();
    });

    // 放大镜
    $(function () {
        $('.zoom').magnify({
            speed: 200,
            // limitBounds: true,
            magnifiedWidth: 1000,
            magnifiedHeight: 1000,
        })
    });

    $(function () {

        // 展会轮播
        $(".processing-lunbo").each(function () {
            var slides = $(this).find(".exhibitionSlide");
            var currentSlide = 0;

            function showSlide() {
                $(slides[currentSlide]).removeClass("borderActive");
                currentSlide = (currentSlide + 1) % slides.length;
                $(slides[currentSlide]).addClass("borderActive");
            }

            setInterval(showSlide, 2300);
        });

        // 展会活动激活状态
        $("#btn01").addClass("active");

        $("#btn01").click(function () {
            $("#btn02").removeClass("active");
            $(this).addClass("active");
            $('#processing').css('display', 'block')
            $('#ended').css('display', 'none')
            // $('#section07').css('height', '125vh')
        });

        $("#btn02").click(function () {
            $("#btn01").removeClass("active");
            $(this).addClass("active");
            $('#processing').css('display', 'none')
            $('#ended').css('display', 'flex')
            $('#ended').css('flex-wrap', 'wrap')
            // $('#section07').css('height', '100vh')
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

    })

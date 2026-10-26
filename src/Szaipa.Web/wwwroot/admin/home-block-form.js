// Small helpers for the 组织架构 / 在售作品 / 合作伙伴 forms. Plain JS, served as-is (no bundling), loaded
// after upload-field.js. Everything is opt-in through data attributes:
//
//  [data-upload-field data-dims-width="ImageWidth" data-dims-height="ImageHeight"]
//      When the preview <img> finishes loading (after an upload, a "带出头像", or the saved image on the edit page),
//      its natural size is written into the two named inputs. The public pages use it for the HTML width/height hint.
//  [data-upload-clear]            (inside the upload field)  empties the image + the size inputs.
//  <select data-artist-select data-avatar-target="AvatarPath" data-fill-name="ArtistName">
//      options carry data-name / data-avatar (the artist's avatar file name under /Content/ArtImg/Artist/).
//      data-fill-name: choosing an artist copies the artist name into that input when it is still empty.
//  [data-artist-pull-avatar]      button: copy the selected artist's avatar into the avatar field (一键带出头像).
(function () {
  'use strict';

  var ARTIST_AVATAR_DIR = '/Content/ArtImg/Artist/';

  function byName(form, name) {
    return name ? form.querySelector('[name="' + name + '"]') : null;
  }

  function wireDimensions(field) {
    var form = field.closest('form') || document;
    var width = byName(form, field.getAttribute('data-dims-width'));
    var height = byName(form, field.getAttribute('data-dims-height'));
    var preview = field.querySelector('[data-upload-preview]');
    if (!preview || !width || !height) return;

    function sync() {
      if (preview.naturalWidth > 0 && preview.naturalHeight > 0) {
        width.value = preview.naturalWidth;
        height.value = preview.naturalHeight;
      }
    }

    preview.addEventListener('load', sync);
    if (preview.complete) sync();
  }

  function wireClear(button) {
    button.addEventListener('click', function () {
      var field = button.closest('[data-upload-field]');
      if (!field) return;
      var form = field.closest('form') || document;
      var hidden = byName(form, field.getAttribute('data-target'));
      var preview = field.querySelector('[data-upload-preview]');
      var status = field.querySelector('[data-upload-status]');
      if (hidden) hidden.value = '';
      var width = byName(form, field.getAttribute('data-dims-width'));
      var height = byName(form, field.getAttribute('data-dims-height'));
      if (width) width.value = '';
      if (height) height.value = '';
      if (preview) {
        preview.removeAttribute('src');
        preview.classList.add('hidden');
      }
      if (status) status.textContent = '已清除';
    });
  }

  function selectedOption(select) {
    return select.options[select.selectedIndex] || null;
  }

  function setAvatar(select, avatarUrl) {
    var form = select.closest('form') || document;
    var targetName = select.getAttribute('data-avatar-target');
    var hidden = byName(form, targetName);
    if (!hidden) return;
    var field = form.querySelector('[data-upload-field][data-target="' + targetName + '"]');
    var preview = field ? field.querySelector('[data-upload-preview]') : null;
    var status = field ? field.querySelector('[data-upload-status]') : null;
    hidden.value = avatarUrl;
    if (preview) {
      preview.src = avatarUrl;
      preview.classList.remove('hidden');
    }
    if (status) status.textContent = '已带出艺术家头像';
  }

  function wireArtistSelect(select) {
    var form = select.closest('form') || document;
    var fillName = byName(form, select.getAttribute('data-fill-name'));

    select.addEventListener('change', function () {
      var option = selectedOption(select);
      if (fillName && option && option.getAttribute('data-name') && !fillName.value) {
        fillName.value = option.getAttribute('data-name');
      }
    });

    var pull = form.querySelector('[data-artist-pull-avatar]');
    if (pull) {
      pull.addEventListener('click', function () {
        var option = selectedOption(select);
        var avatar = option ? option.getAttribute('data-avatar') : '';
        if (!avatar) {
          window.alert('请先选择一位有头像的艺术家。');
          return;
        }
        setAvatar(select, ARTIST_AVATAR_DIR + avatar);
      });
    }
  }

  function init() {
    document.querySelectorAll('[data-upload-field][data-dims-width]').forEach(wireDimensions);
    document.querySelectorAll('[data-upload-clear]').forEach(wireClear);
    document.querySelectorAll('select[data-artist-select]').forEach(wireArtistSelect);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();

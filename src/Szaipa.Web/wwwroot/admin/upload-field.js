// Plain-JS image upload field for the admin backend (cover images etc.). No bundling — served as-is.
// Markup: a [data-upload-field] container with data-upload-url, an [data-upload-trigger], an optional
// [data-upload-preview] <img>, and a hidden input (whose name is data-target). On upload the hidden input
// receives the returned file name and the preview shows the URL.
(function () {
  'use strict';

  function token(scope) {
    var input = (scope.closest('form') || document).querySelector('input[name="__RequestVerificationToken"]')
      || document.querySelector('input[name="__RequestVerificationToken"]');
    return input ? input.value : '';
  }

  function wire(field) {
    if (field.__wired) return;
    field.__wired = true;

    var url = field.getAttribute('data-upload-url') || '/Admin/Upload/Image?folder=newsImg';
    var targetName = field.getAttribute('data-target');
    var hidden = field.querySelector('input[type="hidden"]')
      || (targetName ? document.querySelector('input[name="' + targetName + '"]') : null);
    var preview = field.querySelector('[data-upload-preview]');
    var trigger = field.querySelector('[data-upload-trigger]') || field;
    var status = field.querySelector('[data-upload-status]');

    var picker = document.createElement('input');
    picker.type = 'file';
    picker.accept = 'image/*';
    picker.style.display = 'none';
    field.appendChild(picker);

    trigger.addEventListener('click', function () { picker.click(); });

    picker.addEventListener('change', function () {
      var file = picker.files && picker.files[0];
      if (!file) return;
      var form = new FormData();
      form.append('file', file);
      var t = token(field);
      if (t) form.append('__RequestVerificationToken', t);
      if (status) status.textContent = '上传中…';

      fetch(url, { method: 'POST', body: form, headers: { 'X-Requested-With': 'fetch' } })
        .then(function (r) { return r.json(); })
        .then(function (data) {
          if (!data.success) throw new Error(data.error || '上传失败');
          if (hidden) hidden.value = data.fileName;
          if (preview) {
            preview.src = data.url;
            preview.classList.remove('hidden');
          }
          if (status) status.textContent = '已上传';
        })
        .catch(function (e) {
          if (status) status.textContent = e.message || '上传失败';
        });
    });
  }

  function init(scope) {
    (scope || document).querySelectorAll('[data-upload-field]').forEach(wire);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () { init(); });
  } else {
    init();
  }

  window.SzaipaUploadField = { init: init };
})();

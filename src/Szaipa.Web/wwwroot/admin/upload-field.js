// Plain-JS upload field; optional Cropper.js processing is enabled with data-crop.
(function () {
  'use strict';

  function token(scope) {
    var form = scope.closest('form') || document;
    var input = form.querySelector('input[name="__RequestVerificationToken"]') ||
      document.querySelector('input[name="__RequestVerificationToken"]');
    return input ? input.value : '';
  }

  function wire(field) {
    if (field.__wired) return;
    field.__wired = true;

    var uploadUrl = field.getAttribute('data-upload-url') || '/Staff/Upload/Image?folder=newsImg';
    var storeUrl = field.getAttribute('data-store') === 'url';
    var targetName = field.getAttribute('data-target');
    var hidden = field.querySelector('input[type="hidden"]') ||
      (targetName ? document.querySelector('input[name="' + targetName + '"]') : null);
    var preview = field.querySelector('[data-upload-preview]');
    var trigger = field.querySelector('[data-upload-trigger]') || field;
    var status = field.querySelector('[data-upload-status]');
    var picker = document.createElement('input');
    picker.type = 'file';
    picker.accept = 'image/*';
    picker.hidden = true;
    field.appendChild(picker);

    var folderName = new URL(uploadUrl, location.href).searchParams.get('folder') || 'newsImg';
    var supportsCrop = field.hasAttribute('data-crop');
    var recrop = null;

    function originalLookup(name) {
      var query = '?folder=' + encodeURIComponent(folderName) + '&name=' + encodeURIComponent(name);
      return fetch('/Staff/Upload/Original' + query).then(function (response) {
        if (!response.ok) throw new Error('没有可重新裁切的原图');
        return response.json();
      });
    }

    function select(file) {
      if (!file) return;

      var cropPromise = supportsCrop && window.SzaipaImageCropper
        ? window.SzaipaImageCropper.crop(file, {
          ratio: Number(field.getAttribute('data-crop-ratio')) || 1,
          bwDefault: field.getAttribute('data-crop-bw-default') === 'true'
        })
        : Promise.resolve({ blob: file, file: file });

      cropPromise.then(function (result) {
        if (!result) return;

        var body = new FormData();
        var extension = (file.name.split('.').pop() || 'jpg').toLowerCase();
        var outputExtension = result.blob.type === 'image/jpeg' ? 'jpg' : extension;
        var uploadedFile = new File(
          [result.blob],
          'cropped.' + outputExtension,
          { type: result.blob.type || file.type }
        );

        body.append('file', uploadedFile);
        body.append('folder', folderName);
        if (supportsCrop) body.append('original', file);

        var requestToken = token(field);
        if (requestToken) body.append('__RequestVerificationToken', requestToken);
        if (status) status.textContent = '上传中…';

        fetch(uploadUrl, {
          method: 'POST',
          body: body,
          headers: { 'X-Requested-With': 'fetch' }
        })
          .then(function (response) { return response.json(); })
          .then(function (data) {
            if (!data.success) throw new Error(data.error || '上传失败');

            if (hidden) hidden.value = storeUrl ? data.url : data.fileName;
            if (preview) {
              preview.src = data.url;
              preview.classList.remove('hidden');
            }
            if (recrop) recrop.hidden = !data.originalUrl;
            if (status) status.textContent = data.originalUrl ? '已上传，可重新裁切' : '已上传';
          })
          .catch(function (error) {
            if (status) status.textContent = error.message || '上传失败';
          });
      }).catch(function (error) {
        if (status) status.textContent = error.message || '裁切失败';
      });
    }

    trigger.addEventListener('click', function () { picker.click(); });
    picker.addEventListener('change', function () {
      select(picker.files && picker.files[0]);
      picker.value = '';
    });

    if (supportsCrop) {
      recrop = document.createElement('button');
      recrop.type = 'button';
      recrop.className = 'mt-2 text-xs text-brand hover:underline';
      recrop.textContent = '重新裁切原图';
      recrop.hidden = true;
      field.appendChild(recrop);

      recrop.addEventListener('click', function () {
        if (!hidden || !hidden.value) return;
        var name = decodeURIComponent(hidden.value.split('/').pop());

        originalLookup(name)
          .then(function (data) {
            return fetch(data.url).then(function (response) {
              if (!response.ok) throw new Error('原图读取失败');
              return response.blob().then(function (blob) {
                return { blob: blob, name: data.url.split('/').pop() };
              });
            });
          })
          .then(function (source) {
            select(new File([source.blob], source.name, { type: source.blob.type || 'image/jpeg' }));
          })
          .catch(function (error) {
            if (status) status.textContent = error.message;
          });
      });

      if (hidden && hidden.value) {
        originalLookup(decodeURIComponent(hidden.value.split('/').pop()))
          .then(function () { recrop.hidden = false; })
          .catch(function () {});
      }
    }
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

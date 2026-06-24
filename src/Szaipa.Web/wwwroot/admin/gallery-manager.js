// Numbered-gallery manager for the exhibition admin. Plain JS, no bundling.
// Markup: a [data-gallery] container with data-upload-url, data-list-url, a hidden input (the ordered names),
// a [data-gallery-grid] target, a [data-gallery-add] file input, and data-folder-input = the FolderName field.
// The ordered image names are written (comma-separated) into the hidden input on form submit.
(function () {
  'use strict';

  function token(scope) {
    var i = (scope.closest('form') || document).querySelector('input[name="__RequestVerificationToken"]');
    return i ? i.value : '';
  }

  function wire(root) {
    if (root.__wired) return;
    root.__wired = true;

    var uploadUrl = root.getAttribute('data-upload-url');
    var listUrl = root.getAttribute('data-list-url');
    var hidden = root.querySelector('input[type="hidden"]');
    var grid = root.querySelector('[data-gallery-grid]');
    var addInput = root.querySelector('[data-gallery-add]');
    var status = root.querySelector('[data-gallery-status]');
    var folderInput = document.querySelector(root.getAttribute('data-folder-input') || '[name="FolderName"]');

    var items = []; // { name, url }

    function folder() { return folderInput ? (folderInput.value || '').trim() : ''; }

    function render() {
      grid.innerHTML = '';
      items.forEach(function (it, idx) {
        var cell = document.createElement('div');
        cell.className = 'gallery-cell';
        cell.innerHTML =
          '<img src="' + it.url + '" alt="" />' +
          '<div class="gallery-cell__bar">' +
          '<button type="button" title="前移" data-act="up">↑</button>' +
          '<span>' + (idx + 1) + '</span>' +
          '<button type="button" title="后移" data-act="down">↓</button>' +
          '<button type="button" title="删除" data-act="del">✕</button>' +
          '</div>';
        cell.querySelector('[data-act="up"]').onclick = function () { move(idx, -1); };
        cell.querySelector('[data-act="down"]').onclick = function () { move(idx, 1); };
        cell.querySelector('[data-act="del"]').onclick = function () { items.splice(idx, 1); render(); };
        grid.appendChild(cell);
      });
      hidden.value = items.map(function (it) { return it.name; }).join(',');
    }

    function move(idx, delta) {
      var to = idx + delta;
      if (to < 0 || to >= items.length) return;
      var tmp = items[idx]; items[idx] = items[to]; items[to] = tmp;
      render();
    }

    function uploadOne(file) {
      var f = folder();
      if (!f) { if (status) status.textContent = '请先填写图片文件夹名'; return Promise.resolve(); }
      var form = new FormData();
      form.append('file', file);
      var t = token(root);
      if (t) form.append('__RequestVerificationToken', t);
      return fetch(uploadUrl + '?folder=' + encodeURIComponent(f), { method: 'POST', body: form })
        .then(function (r) { return r.json(); })
        .then(function (data) {
          if (data.success) { items.push({ name: data.name, url: data.url }); render(); }
          else if (status) { status.textContent = data.error || '上传失败'; }
        });
    }

    if (addInput) {
      addInput.addEventListener('change', function () {
        var files = Array.prototype.slice.call(addInput.files || []);
        if (status) status.textContent = '上传中…';
        files.reduce(function (p, file) { return p.then(function () { return uploadOne(file); }); }, Promise.resolve())
          .then(function () { if (status) status.textContent = items.length + ' 张图片'; addInput.value = ''; });
      });
    }

    // Edit page: load existing gallery for the current folder.
    if (listUrl && folder()) {
      fetch(listUrl + '?folder=' + encodeURIComponent(folder()))
        .then(function (r) { return r.json(); })
        .then(function (list) { items = (list || []).map(function (i) { return { name: i.name, url: i.url }; }); render(); });
    }

    // Keep the hidden value current on submit.
    var form = root.closest('form');
    if (form) form.addEventListener('submit', render);

    render();
  }

  function init(scope) { (scope || document).querySelectorAll('[data-gallery]').forEach(wire); }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () { init(); });
  } else { init(); }

  window.SzaipaGallery = { init: init };
})();

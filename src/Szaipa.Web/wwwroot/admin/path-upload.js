// 给存「/Content/... 路径」的输入框加「上传图片」按钮：
//   <input data-path-upload>       上传后替换为新地址
//   <textarea data-path-upload>    上传后追加一行（可多选），适合「每行一个路径」的字段
// 可选 data-upload-folder（默认 Publication）。下方自动显示缩略图预览，便于确认路径是否有效。
(function () {
  'use strict';

  function token() {
    var input = document.querySelector('input[name="__RequestVerificationToken"]');
    return input ? input.value : '';
  }

  function paths(field) {
    return field.value.split(/\r?\n/).map(function (line) { return line.trim(); }).filter(Boolean);
  }

  function wire(field) {
    var multi = field.tagName === 'TEXTAREA';
    var folder = field.getAttribute('data-upload-folder') || 'Publication';

    var bar = document.createElement('div');
    bar.className = 'mt-2 flex flex-wrap items-center gap-2';
    var button = document.createElement('button');
    button.type = 'button';
    button.className = 'admin-btn-ghost px-3 py-1';
    button.textContent = multi ? '上传图片（可多选）' : '上传图片';
    var status = document.createElement('span');
    status.className = 'text-xs text-muted';
    var picker = document.createElement('input');
    picker.type = 'file';
    picker.accept = 'image/*';
    picker.multiple = multi;
    picker.hidden = true;
    var previews = document.createElement('div');
    previews.className = 'mt-2 flex flex-wrap gap-2';

    function renderPreviews() {
      previews.replaceChildren();
      paths(field).slice(0, 12).forEach(function (src) {
        var img = document.createElement('img');
        img.src = src;
        img.alt = '';
        img.className = 'h-16 w-16 rounded border border-black/10 object-cover';
        img.onerror = function () { img.className += ' opacity-30'; img.title = '找不到该图片：' + src; };
        previews.append(img);
      });
    }

    function upload(file) {
      var body = new FormData();
      body.append('file', file);
      body.append('folder', folder);
      body.append('__RequestVerificationToken', token());
      return fetch('/Staff/Upload/Image?folder=' + encodeURIComponent(folder), {
        method: 'POST',
        body: body,
        headers: { 'X-Requested-With': 'fetch' }
      })
        .then(function (response) { return response.json(); })
        .then(function (data) {
          if (!data.success) throw new Error(data.error || '上传失败');
          return data.url;
        });
    }

    button.addEventListener('click', function () { picker.click(); });
    picker.addEventListener('change', function () {
      var files = Array.prototype.slice.call(picker.files || []);
      picker.value = '';
      if (!files.length) return;
      status.textContent = '上传中…';
      files.reduce(function (chain, file) {
        return chain.then(function () {
          return upload(file).then(function (url) {
            field.value = multi ? paths(field).concat(url).join('\n') : url;
          });
        });
      }, Promise.resolve())
        .then(function () { status.textContent = '已上传 ' + files.length + ' 张'; })
        .catch(function (error) { status.textContent = error.message || '上传失败'; })
        .then(renderPreviews);
    });
    field.addEventListener('input', renderPreviews);

    bar.append(button, status, picker);
    field.after(bar, previews);
    renderPreviews();
  }

  document.querySelectorAll('[data-path-upload]').forEach(wire);
})();

import Cropper from 'cropperjs';
import 'cropperjs/dist/cropper.css';

(function () {
  'use strict';
  function crop(file, options) {
    return new Promise(function (resolve, reject) {
      var url = URL.createObjectURL(file);
      var overlay = document.createElement('div');
      overlay.className = 'szaipa-cropper-overlay';
      overlay.innerHTML = '<div class="szaipa-cropper-dialog"><div class="szaipa-cropper-image"><img alt="待裁切图片"></div><label><input type="checkbox" data-bw> 转为黑白</label><div class="szaipa-cropper-actions"><button type="button" data-cancel>取消</button><button type="button" data-confirm>使用裁切图</button></div></div>';
      var img = overlay.querySelector('img'); img.src = url; overlay.querySelector('[data-bw]').checked = !!options.bwDefault;
      var style = document.createElement('style');
      style.textContent = '.szaipa-cropper-overlay{position:fixed;inset:0;z-index:9999;background:#0009;display:grid;place-items:center;padding:16px}.szaipa-cropper-dialog{background:white;border-radius:12px;padding:16px;max-width:min(900px,96vw);max-height:94vh;display:flex;flex-direction:column;gap:12px}.szaipa-cropper-image{height:min(65vh,650px);width:min(820px,90vw)}.szaipa-cropper-image img{display:block;max-width:100%;max-height:100%}.szaipa-cropper-actions{display:flex;justify-content:flex-end;gap:8px}.szaipa-cropper-actions button{border:1px solid #ccc;border-radius:6px;padding:8px 12px}';
      document.head.appendChild(style); document.body.appendChild(overlay);
      var cropper;
      img.onload = function () { cropper = new Cropper(img, { viewMode: 1, autoCropArea: .9, aspectRatio: options.ratio || 1, responsive: true }); };
      function done(result) { if (cropper) cropper.destroy(); URL.revokeObjectURL(url); overlay.remove(); style.remove(); result ? resolve(result) : resolve(null); }
      overlay.querySelector('[data-cancel]').onclick = function () { done(null); };
      overlay.querySelector('[data-confirm]').onclick = function () {
        if (!cropper) return;
        var canvas = cropper.getCroppedCanvas({ maxWidth: 2400, maxHeight: 2400, imageSmoothingQuality: 'high' });
        if (overlay.querySelector('[data-bw]').checked) {
          var ctx = canvas.getContext('2d'); var pixels = ctx.getImageData(0, 0, canvas.width, canvas.height);
          for (var i = 0; i < pixels.data.length; i += 4) { var y = Math.round(.299 * pixels.data[i] + .587 * pixels.data[i + 1] + .114 * pixels.data[i + 2]); pixels.data[i] = pixels.data[i + 1] = pixels.data[i + 2] = y; }
          ctx.putImageData(pixels, 0, 0);
        }
        canvas.toBlob(function (blob) { if (!blob) return reject(new Error('无法生成裁切图')); done({ blob: blob, file: file }); }, 'image/jpeg', .92);
      };
    });
  }
  window.SzaipaImageCropper = { crop: crop };
})();

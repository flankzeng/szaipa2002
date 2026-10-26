// Plain-JS upload field; optional Cropper.js processing is enabled with data-crop.
(function () {
  'use strict';
  function token(scope) { var input=(scope.closest('form')||document).querySelector('input[name="__RequestVerificationToken"]')||document.querySelector('input[name="__RequestVerificationToken"]'); return input?input.value:''; }
  function wire(field) {
    if(field.__wired)return; field.__wired=true;
    var uploadUrl=field.getAttribute('data-upload-url')||'/Staff/Upload/Image?folder=newsImg';
    var storeUrl=field.getAttribute('data-store')==='url', targetName=field.getAttribute('data-target');
    var hidden=field.querySelector('input[type="hidden"]')||(targetName?document.querySelector('input[name="'+targetName+'"]'):null);
    var preview=field.querySelector('[data-upload-preview]'), trigger=field.querySelector('[data-upload-trigger]')||field, status=field.querySelector('[data-upload-status]');
    var picker=document.createElement('input'); picker.type='file'; picker.accept='image/*'; picker.hidden=true; field.appendChild(picker);
    var folderName=new URL(uploadUrl,location.href).searchParams.get('folder')||'newsImg';
    var recrop=null;
    function originalLookup(name) {
      return fetch('/Staff/Upload/Original?folder='+encodeURIComponent(folderName)+'&name='+encodeURIComponent(name))
        .then(function(r){if(!r.ok)throw new Error('没有可重新裁切的原图');return r.json();});
    }
    function select(file) {
      if(!file)return;
      var cropPromise=field.hasAttribute('data-crop')&&window.SzaipaImageCropper
        ? window.SzaipaImageCropper.crop(file,{ratio:Number(field.getAttribute('data-crop-ratio'))||1,bwDefault:field.getAttribute('data-crop-bw-default')==='true'})
        : Promise.resolve({blob:file,file:file});
      cropPromise.then(function(result){ if(!result)return; var body=new FormData();
        var ext=(file.name.split('.').pop()||'jpg').toLowerCase();
        var outputExt=result.blob.type==='image/jpeg'?'jpg':ext;
        body.append('file',new File([result.blob],'cropped.'+outputExt,{type:result.blob.type||file.type}));
        body.append('folder',folderName); body.append('original',file);
        var t=token(field); if(t)body.append('__RequestVerificationToken',t); if(status)status.textContent='上传中…';
        fetch(uploadUrl,{method:'POST',body:body,headers:{'X-Requested-With':'fetch'}}).then(function(r){return r.json();}).then(function(data){if(!data.success)throw new Error(data.error||'上传失败');
          if(hidden)hidden.value=storeUrl?data.url:data.fileName; if(preview){preview.src=data.url;preview.classList.remove('hidden');} if(recrop)recrop.hidden=!data.originalUrl; if(status)status.textContent=data.originalUrl?'已上传，可重新裁切':'已上传';
        }).catch(function(e){if(status)status.textContent=e.message||'上传失败';});
      }).catch(function(e){if(status)status.textContent=e.message||'裁切失败';});
    }
    trigger.addEventListener('click',function(){picker.click();}); picker.addEventListener('change',function(){select(picker.files&&picker.files[0]);picker.value='';});
    if(field.hasAttribute('data-crop')) {
      recrop=document.createElement('button'); recrop.type='button'; recrop.className='mt-2 text-xs text-brand hover:underline'; recrop.textContent='重新裁切原图'; recrop.hidden=true; field.appendChild(recrop);
      recrop.addEventListener('click',function(){ if(!hidden||!hidden.value)return; var name=decodeURIComponent(hidden.value.split('/').pop());
        originalLookup(name).then(function(data){return fetch(data.url).then(function(r){if(!r.ok)throw new Error('原图读取失败');return r.blob().then(function(blob){return {blob:blob,name:data.url.split('/').pop()};});});}).then(function(source){select(new File([source.blob],source.name,{type:source.blob.type||'image/jpeg'}));}).catch(function(e){if(status)status.textContent=e.message;});
      });
      if(hidden&&hidden.value)originalLookup(decodeURIComponent(hidden.value.split('/').pop())).then(function(){recrop.hidden=false;}).catch(function(){});
    }
  }
  function init(scope){(scope||document).querySelectorAll('[data-upload-field]').forEach(wire);}
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',function(){init();});else init();
  window.SzaipaUploadField={init:init};
})();

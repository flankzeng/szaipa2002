// TipTap rich-text editor for the staff admin backend, replacing the dead layui `layedit`.
// Bundled to ../editor.js by `npm run build:js` (esbuild, IIFE global `SzaipaEditor`).
//
// Usage: render the _RichTextEditor partial, which emits a [data-editor] container with a hidden input.
// On DOMContentLoaded every [data-editor] is mounted; the editor keeps its hidden input's value in sync
// with the HTML so a normal form POST submits the content (stored as HTML, same as the legacy `content`).
import { Editor } from '@tiptap/core';
import StarterKit from '@tiptap/starter-kit';
import Image from '@tiptap/extension-image';
import Link from '@tiptap/extension-link';
import Underline from '@tiptap/extension-underline';
import TextAlign from '@tiptap/extension-text-align';
import Placeholder from '@tiptap/extension-placeholder';

const BTN = 'admin-editor__btn';
const BTN_ACTIVE = 'is-active';

function button(label, title) {
  const b = document.createElement('button');
  b.type = 'button';
  b.className = BTN;
  b.title = title;
  b.innerHTML = label;
  return b;
}

function divider() {
  const d = document.createElement('span');
  d.className = 'admin-editor__divider';
  return d;
}

function antiForgeryToken(root) {
  // The surrounding form renders @Html.AntiForgeryToken(); send it as the default form field name.
  const input = (root.closest('form') || document).querySelector('input[name="__RequestVerificationToken"]')
    || document.querySelector('input[name="__RequestVerificationToken"]');
  return input ? input.value : '';
}

async function uploadImage(root, file) {
  const url = root.getAttribute('data-upload-url') || '/Staff/Upload/Image?folder=newsImg';
  const form = new FormData();
  form.append('file', file);
  const token = antiForgeryToken(root);
  if (token) form.append('__RequestVerificationToken', token);

  const res = await fetch(url, { method: 'POST', body: form, headers: { 'X-Requested-With': 'fetch' } });
  if (!res.ok) throw new Error('upload failed: ' + res.status);
  const data = await res.json();
  if (!data.success) throw new Error(data.error || '上传失败');
  return data.url;
}

function buildToolbar(editor, toolbar, root) {
  const groups = [
    [
      ['撤销', '撤销', () => editor.chain().focus().undo().run()],
      ['重做', '重做', () => editor.chain().focus().redo().run()],
    ],
    [
      ['H2', '标题', () => editor.chain().focus().toggleHeading({ level: 2 }).run(), () => editor.isActive('heading', { level: 2 })],
      ['H3', '小标题', () => editor.chain().focus().toggleHeading({ level: 3 }).run(), () => editor.isActive('heading', { level: 3 })],
    ],
    [
      ['<b>B</b>', '加粗', () => editor.chain().focus().toggleBold().run(), () => editor.isActive('bold')],
      ['<i>I</i>', '斜体', () => editor.chain().focus().toggleItalic().run(), () => editor.isActive('italic')],
      ['<u>U</u>', '下划线', () => editor.chain().focus().toggleUnderline().run(), () => editor.isActive('underline')],
      ['<s>S</s>', '删除线', () => editor.chain().focus().toggleStrike().run(), () => editor.isActive('strike')],
    ],
    [
      ['• 列表', '无序列表', () => editor.chain().focus().toggleBulletList().run(), () => editor.isActive('bulletList')],
      ['1. 列表', '有序列表', () => editor.chain().focus().toggleOrderedList().run(), () => editor.isActive('orderedList')],
      ['❝', '引用', () => editor.chain().focus().toggleBlockquote().run(), () => editor.isActive('blockquote')],
    ],
    [
      ['左', '左对齐', () => editor.chain().focus().setTextAlign('left').run(), () => editor.isActive({ textAlign: 'left' })],
      ['中', '居中', () => editor.chain().focus().setTextAlign('center').run(), () => editor.isActive({ textAlign: 'center' })],
      ['右', '右对齐', () => editor.chain().focus().setTextAlign('right').run(), () => editor.isActive({ textAlign: 'right' })],
    ],
    [
      ['链接', '插入/编辑链接', () => setLink(editor), () => editor.isActive('link')],
      ['图片', '插入图片', () => pickImage(editor, root)],
      ['清除', '清除格式', () => editor.chain().focus().unsetAllMarks().clearNodes().run()],
    ],
  ];

  const buttons = [];
  groups.forEach((group, gi) => {
    group.forEach(([label, title, onClick, isActive]) => {
      const b = button(label, title);
      b.addEventListener('click', onClick);
      toolbar.appendChild(b);
      buttons.push({ el: b, isActive });
    });
    if (gi < groups.length - 1) toolbar.appendChild(divider());
  });

  const refresh = () => buttons.forEach(({ el, isActive }) => {
    if (isActive) el.classList.toggle(BTN_ACTIVE, !!isActive());
  });
  editor.on('selectionUpdate', refresh);
  editor.on('transaction', refresh);
  refresh();
}

function setLink(editor) {
  const previous = editor.getAttributes('link').href || '';
  const href = window.prompt('链接地址（留空取消链接）：', previous);
  if (href === null) return;
  if (href === '') {
    editor.chain().focus().extendMarkRange('link').unsetLink().run();
    return;
  }
  editor.chain().focus().extendMarkRange('link').setLink({ href }).run();
}

function pickImage(editor, root) {
  const input = document.createElement('input');
  input.type = 'file';
  input.accept = 'image/*';
  input.addEventListener('change', async () => {
    const file = input.files && input.files[0];
    if (!file) return;
    try {
      const url = await uploadImage(root, file);
      editor.chain().focus().setImage({ src: url }).run();
    } catch (e) {
      window.alert(e.message || '图片上传失败');
    }
  });
  input.click();
}

export function mount(root) {
  if (!root || root.__mounted) return null;
  root.__mounted = true;

  const surface = root.querySelector('[data-editor-surface]');
  const toolbar = root.querySelector('[data-editor-toolbar]');
  const fieldId = root.getAttribute('data-field');
  const hidden = fieldId ? document.getElementById(fieldId) : null;
  const placeholder = root.getAttribute('data-placeholder') || '在此撰写正文…';

  const editor = new Editor({
    element: surface,
    extensions: [
      StarterKit,
      Underline,
      Link.configure({ openOnClick: false, autolink: true }),
      Image.configure({ inline: false, HTMLAttributes: { class: 'admin-editor__img' } }),
      TextAlign.configure({ types: ['heading', 'paragraph'] }),
      Placeholder.configure({ placeholder }),
    ],
    content: hidden ? hidden.value : '',
    onUpdate: ({ editor }) => {
      if (hidden) hidden.value = editor.getHTML();
    },
  });

  if (toolbar) buildToolbar(editor, toolbar, root);

  // Keep the hidden field current even if the form is submitted without a final keystroke event.
  const form = root.closest('form');
  if (form && hidden) {
    form.addEventListener('submit', () => { hidden.value = editor.getHTML(); });
  }

  return editor;
}

export function init(scope) {
  (scope || document).querySelectorAll('[data-editor]').forEach(mount);
}

if (typeof document !== 'undefined') {
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => init());
  } else {
    init();
  }
}

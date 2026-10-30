#!/usr/bin/env bash
# 大 GIF 转静音循环视频（2026-10-30）。
# 在一份 Content 目录副本里，给 ≥ 300KB、还没有旁挂文件的 GIF 生成：
#   xxx.gif.mp4  H.264 / yuv420p / CRF 28 / 无音轨 / faststart，帧数与时长与原 GIF 一致
#   xxx.gif.jpg  首帧，作为 <video poster>
# 原 GIF 不改。生成后把新文件按相同相对路径放回服务器 Content 目录即可，
# 网站（ImageVariants.GifVideoTag）发现旁挂文件后自动改用视频输出。
# 用法：scripts/gif-to-video.sh <Content 目录> [最小字节数，默认 300000]
set -euo pipefail

root="${1:?用法：$0 <Content 目录> [最小字节数]}"
min_bytes="${2:-300000}"
command -v ffmpeg >/dev/null || { echo "需要 ffmpeg（brew install ffmpeg）" >&2; exit 1; }

find "$root" -type f -iname '*.gif' -size +"$((min_bytes / 1024))"k -print0 |
while IFS= read -r -d '' gif; do
  [ -f "$gif.mp4" ] && continue
  # 带透明的 GIF 转 MP4 会变成黑底，跳过。
  if ffmpeg -nostdin -v error -i "$gif" -vf "alphaextract,signalstats,metadata=mode=print:key=lavfi.signalstats.YMIN:file=-" -f null - 2>/dev/null |
     grep 'YMIN=' | grep -qv 'YMIN=255$'; then
    echo "跳过（含透明）：$gif"
    continue
  fi
  ffmpeg -nostdin -v error -y -i "$gif" -an -fps_mode vfr -c:v libx264 -preset slow -crf 28 \
    -pix_fmt yuv420p -vf "scale=trunc(iw/2)*2:trunc(ih/2)*2" -movflags +faststart "$gif.mp4"
  ffmpeg -nostdin -v error -y -i "$gif" -frames:v 1 -q:v 8 "$gif.jpg"
  echo "$(stat -f %z "$gif" 2>/dev/null || stat -c %s "$gif") → $(stat -f %z "$gif.mp4" 2>/dev/null || stat -c %s "$gif.mp4")  $gif"
done

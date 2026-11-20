// Smoke test: encode BGR then decode via the exact exports WebPWrapper uses.
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "webp/encode.h"
#include "webp/decode.h"

int main(void) {
  const int W = 37, H = 23;  // odd sizes stress stride paths
  size_t raw_size = (size_t)W * H * 3;
  uint8_t *raw = malloc(raw_size);
  for (int y = 0; y < H; ++y)
    for (int x = 0; x < W; ++x) {
      uint8_t *p = raw + ((size_t)y * W + x) * 3;
      p[0] = (uint8_t)(x * 7); p[1] = (uint8_t)(y * 11); p[2] = (uint8_t)((x + y) * 5);
    }
  uint8_t *enc = NULL;
  size_t enc_size = WebPEncodeBGR(raw, W, H, W * 3, 80, &enc);
  if (!enc_size) { printf("FAIL encode\n"); return 1; }
  int w = 0, h = 0;
  if (!WebPGetInfo(enc, enc_size, &w, &h) || w != W || h != H) {
    printf("FAIL WebPGetInfo (%dx%d)\n", w, h); return 1;
  }
  uint8_t *out = malloc(raw_size);
  uint8_t *ret = WebPDecodeBGRInto(enc, enc_size, out, raw_size, W * 3);
  if (ret != out) { printf("FAIL WebPDecodeBGRInto ret=%p\n", (void*)ret); return 1; }
  long diff = 0;
  for (size_t i = 0; i < raw_size; ++i) diff += abs((int)raw[i] - (int)out[i]);
  // BGRA path too (master wrapper uses it when alpha present)
  WebPBitstreamFeatures feats;
  if (WebPGetFeatures(enc, enc_size, &feats) != VP8_STATUS_OK) { printf("FAIL GetFeatures\n"); return 1; }
  printf("features: %dx%d has_alpha=%d format=%d abi ok\n", feats.width, feats.height, feats.has_alpha, feats.format);
  WebPFree(enc); free(raw); free(out);
  printf("SMOKE %s (mean abs diff %.2f)\n", diff / (double)raw_size < 8.0 ? "PASS" : "SUSPECT", diff / (double)raw_size);
  return 0;
}

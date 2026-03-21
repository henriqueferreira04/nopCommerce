import http from "k6/http";
import { check, sleep } from "k6";

// ---------------------------------------------------------------------------
// Load profile: ramp to 20 VUs, sustain 3 min, ramp down — ~4 min total
// ---------------------------------------------------------------------------
export const options = {
  stages: [
    { duration: "30s", target: 20 },  // ramp up
    { duration: "3m",  target: 20 },  // sustain
    { duration: "30s", target: 0 },   // ramp down
  ],
  thresholds: {
    http_req_failed: ["rate<0.10"],      // <10 % errors
    http_req_duration: ["p(95)<5000"],   // p95 under 5 s
  },
};

const BASE = __ENV.BASE_URL || "http://localhost";

// nopCommerce default sample-data slugs
const SEARCH_TERMS      = ["computer", "phone", "camera", "book", "shoes", "laptop"];
const NO_RESULT_TERMS   = ["xyznonexistent", "qqqqnotfound", "zzznoitem"];
const CATEGORY_SLUGS    = ["computers", "desktops", "electronics", "apparel", "shoes"];
const PRODUCT_SLUGS     = [
  "build-your-own-computer",
  "apple-macbook-pro-13-inch",
  "obey-propaganda-hat",
];

function pick(arr) {
  return arr[Math.floor(Math.random() * arr.length)];
}

// ---------------------------------------------------------------------------
// Virtual-user scenario: simulates a shopper browsing the store
// ---------------------------------------------------------------------------
export default function () {
  // 1. Homepage
  let res = http.get(`${BASE}/`);
  check(res, { "homepage 200": (r) => r.status === 200 });
  sleep(randomThink());

  // 2. Search with results  →  triggers search_performed metric + Catalog.Search trace
  const term = pick(SEARCH_TERMS);
  res = http.get(`${BASE}/search?q=${term}`);
  check(res, { "search 200": (r) => r.status === 200 });
  sleep(randomThink());

  // 3. Search with NO results (~20% of the time)  →  triggers search_no_results metric
  if (Math.random() < 0.2) {
    const noTerm = pick(NO_RESULT_TERMS);
    res = http.get(`${BASE}/search?q=${noTerm}`);
    check(res, { "no-result search 200": (r) => r.status === 200 });
    sleep(randomThink());
  }

  // 4. Search autocomplete  →  triggers Catalog.SearchAutoComplete trace
  res = http.get(`${BASE}/catalog/searchtermautocomplete?term=${term.substring(0, 3)}`);
  check(res, { "autocomplete 200": (r) => r.status === 200 });
  sleep(randomThink());

  // 5. Category page  →  triggers Catalog.Category trace
  res = http.get(`${BASE}/${pick(CATEGORY_SLUGS)}`);
  check(res, { "category 200": (r) => r.status === 200 });
  sleep(randomThink());

  // 6. Product detail page  →  triggers Catalog.ProductDetails trace + pricing cache hit/miss
  res = http.get(`${BASE}/${pick(PRODUCT_SLUGS)}`);
  check(res, { "product 200": (r) => r.status === 200 });
  sleep(randomThink());
}

// Random think-time between 1 and 3 seconds
function randomThink() {
  return 1 + Math.random() * 2;
}

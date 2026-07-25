const endpoint = "https://speed.johnnyli.dev/network-diagnostics-speed-v1.bin";
const expectedBytes = 256 * 1024 * 1024;
const origin = "https://network.johnnyli.dev";

function requireHeader(response, name) {
  const value = response.headers.get(name);
  if (!value) throw new Error(`${name} was not returned by ${endpoint}.`);
  return value;
}

function requireTimingAccess(response) {
  const value = requireHeader(response, "Timing-Allow-Origin");
  if (value !== origin && value !== "*") {
    throw new Error(`Timing-Allow-Origin did not permit ${origin}.`);
  }
}

const head = await fetch(endpoint, {
  method: "HEAD",
  headers: { Origin: origin }
});
if (!head.ok) throw new Error(`HEAD returned ${head.status}.`);
if (Number.parseInt(requireHeader(head, "Content-Length"), 10) !== expectedBytes) {
  throw new Error(`HEAD did not report ${expectedBytes} bytes.`);
}
if (requireHeader(head, "Access-Control-Allow-Origin") !== origin) {
  throw new Error("The R2 CORS policy did not allow the production app origin.");
}
requireTimingAccess(head);

const range = await fetch(endpoint, {
  headers: {
    Origin: origin,
    Range: "bytes=0-1023"
  }
});
if (range.status !== 206) throw new Error(`Range request returned ${range.status}; expected 206.`);
if (requireHeader(range, "Content-Range") !== `bytes 0-1023/${expectedBytes}`) {
  throw new Error("The R2 endpoint returned an unexpected Content-Range.");
}
requireTimingAccess(range);
if ((await range.arrayBuffer()).byteLength !== 1024) {
  throw new Error("The R2 endpoint did not return the requested 1024-byte range.");
}

console.log(JSON.stringify({
  endpoint,
  headCacheStatus: head.headers.get("CF-Cache-Status"),
  rangeCacheStatus: range.headers.get("CF-Cache-Status"),
  age: range.headers.get("Age"),
  timingAllowOrigin: range.headers.get("Timing-Allow-Origin")
}, null, 2));

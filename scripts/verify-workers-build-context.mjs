const isWorkersBuild = process.env.WORKERS_CI === "1";
const branch = process.env.WORKERS_CI_BRANCH?.trim();

if (isWorkersBuild && branch && branch !== "main") {
  console.error(
    [
      `Refusing Cloudflare Workers build from non-production branch: ${branch}`,
      "network.johnnyli.dev is production-only from main.",
      "Disable non-production Workers Builds or configure their deploy command as `npx wrangler versions upload` before re-enabling branch previews.",
    ].join("\n"),
  );
  process.exit(1);
}

if (isWorkersBuild && !branch) {
  console.error("WORKERS_CI is set but WORKERS_CI_BRANCH is unavailable; refusing an ambiguous production build.");
  process.exit(1);
}

import { resolveConsumerRelease } from "./design-system-consumer-release.mjs";

const release = await resolveConsumerRelease();
console.log(`Locked ${release.package} v${release.version} at ${release.sourceCommit}.`);

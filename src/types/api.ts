export interface EdgeMetadata {
  edge: string | null;
  network: string | null;
  asn: number | null;
  protocol: string | null;
  tlsVersion: string | null;
  ipVersion: "IPv4" | "IPv6" | "Unknown";
}

export interface UploadReceipt {
  bytes: number;
}

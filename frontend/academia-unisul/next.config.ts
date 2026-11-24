import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  /* config options here */

  async redirects() {
    return [{ source: "/", destination: "/login", permanent: false }];
  },

  async rewrites() {
    return [
      {
        source: "/api/:path*",
        destination: "http://localhost:5172/:path*",
      },
    ];
  },

  experimental: {
    agentFeedback: true,
  },
  cacheComponents: true,
  partialPrefetching: true,
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
};

export default nextConfig;
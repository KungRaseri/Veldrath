FROM node:22-alpine AS build
WORKDIR /opt/app

# Install dependencies separately for layer caching
COPY package.json package-lock.json* ./
RUN npm ci

# Copy Strapi project files
COPY . .

# Build the Strapi admin panel
RUN npm run build

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM node:22-alpine AS final
WORKDIR /opt/app

ENV NODE_ENV=production

# Copy built artefacts from the build stage
COPY --from=build /opt/app ./

EXPOSE 1337
CMD ["npm", "start"]

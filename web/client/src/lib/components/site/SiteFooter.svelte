<script lang="ts">
	import { resolve } from '$app/paths';
	import { brandDiscordPath, brandGithubPath } from '#lib/brand-icons';

	let { user = null }: { user?: AuthUser | null } = $props();
</script>

<footer class="site-footer">
	<div class="site-footer-inner">
		<div class="grid gap-8 md:grid-cols-4">
			<div class="space-y-3 md:col-span-1">
				<img
					class="h-16 w-auto"
					src="/images/veldrath-final-horizontal-transparent.png"
					alt="Veldrath"
				/>
				<p class="max-w-xs text-sm leading-relaxed text-surface-600-400">
					A multiplayer RPG built on the RealmEngine framework. Explore, craft, fight, and build
					your legend in the world of Veldrath.
				</p>
			</div>

			<div>
				<h2 class="footer-heading">Quick Links</h2>
				<ul class="space-y-1">
					<li><a class="footer-link" href={resolve('/lore')}>World & Lore</a></li>
					<li><a class="footer-link" href={resolve('/patch-notes')}>Patch Notes</a></li>
					<li><a class="footer-link" href={resolve('/community')}>Community</a></li>
					{#if user}
						<li><a class="footer-link" href={resolve('/characters')}>Play</a></li>
						<li><a class="footer-link" href={resolve('/account')}>Account</a></li>
					{:else}
						<li><a class="footer-link" href={resolve('/login')}>Sign In</a></li>
					{/if}
				</ul>
			</div>

			<div>
				<h2 class="footer-heading">Resources</h2>
				<ul class="space-y-1">
					<li>
						<a
							class="footer-link"
							href="https://kungraseri.github.io/RealmEngine/"
							target="_blank"
							rel="noreferrer">Documentation</a
						>
					</li>
					<li>
						<a
							class="footer-link"
							href="https://github.com/kungraseri/RealmEngine/wiki"
							target="_blank"
							rel="noreferrer">Wiki</a
						>
					</li>
					<li><a class="footer-link" href={resolve('/')}>Support</a></li>
				</ul>
			</div>

			<div>
				<h2 class="footer-heading">Community</h2>
				<ul class="space-y-1">
					<li>
						<a
							class="footer-link"
							href="https://github.com/kungraseri/RealmEngine"
							target="_blank"
							rel="noreferrer"
						>
							<svg
								width="16"
								height="16"
								viewBox="0 0 24 24"
								fill="currentColor"
								aria-hidden="true"
							>
								<path d={brandGithubPath} />
							</svg>
							<span>GitHub</span>
						</a>
					</li>
					<li>
						<a
							class="footer-link"
							href="https://discord.gg/veldrath"
							target="_blank"
							rel="noreferrer"
						>
							<svg
								width="16"
								height="16"
								viewBox="0 0 24 24"
								fill="currentColor"
								aria-hidden="true"
							>
								<path d={brandDiscordPath} />
							</svg>
							<span>Discord</span>
						</a>
					</li>
				</ul>
			</div>
		</div>

		<div class="footer-divider"></div>

		<div class="flex flex-wrap items-center justify-between gap-3 text-xs text-surface-600-400">
			<p>&copy; {new Date().getFullYear()} Veldrath. All rights reserved.</p>
			<p class="flex gap-4">
				<a class="footer-link" href={resolve('/')}>Privacy Policy</a>
				<a class="footer-link" href={resolve('/')}>Terms of Service</a>
				<a class="footer-link" href={resolve('/')}>Contact</a>
			</p>
		</div>
	</div>
</footer>

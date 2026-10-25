import type { CapacitorConfig } from '@capacitor/cli';

/**
 * Gravelio's SPA and API must share one origin: the auth cookie is SameSite=Strict and Angular reads the
 * XSRF token from `document.cookie`. So the Android app is a WebView around the instance you deploy,
 * not the bundled assets calling a remote API. Set the instance for every sync:
 *
 *   GRAVELIO_SERVER_URL=https://gravelio.example pnpm cap:sync
 */
const serverUrl = process.env['GRAVELIO_SERVER_URL'];
if (!serverUrl) {
  throw new Error(
    'Set GRAVELIO_SERVER_URL to your Gravelio instance, e.g. ' +
      'GRAVELIO_SERVER_URL=https://gravelio.example pnpm cap:sync',
  );
}

const config: CapacitorConfig = {
  appId: 'io.gravelio.app',
  appName: 'Gravelio',
  webDir: 'dist/frontend/browser',
  // Self-hosted on a LAN is often plain http, which Android blocks unless the app opts in.
  server: { url: serverUrl, cleartext: serverUrl.startsWith('http://') },
};

export default config;

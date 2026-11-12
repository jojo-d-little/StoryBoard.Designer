// Uses an existing Playwright installation; does not install or change sibling packages.
const fs = require("node:fs/promises");
const path = require("node:path");
const [playwrightModule, uri, evidenceDirectory, expectedText] = process.argv.slice(2);
const { chromium } = require(playwrightModule);

(async () => {
  await fs.mkdir(evidenceDirectory, { recursive: true });
  const browser = await chromium.launch({ headless: true, args: ["--autoplay-policy=user-gesture-required"] });
  const page = await browser.newPage();
  const failures = [];
  page.on("pageerror", error => failures.push(error.message));
  try {
    const sessionResponse = page.waitForResponse(response =>
      new URL(response.url()).pathname === "/api/v1/session/start", { timeout: 30000 });
    // Attach a rejection handler while the startup gesture is still pending.
    sessionResponse.catch(() => {});
    await page.goto(uri);
    await page.getByRole("button", { name: "Continue with sound disabled", exact: true }).click();
    const session = await (await sessionResponse).json();
    if (!session.result?.success || !session.session?.sessionId) {
      throw new Error(`Portal session start failed: ${JSON.stringify(session.result)}`);
    }
    await page.getByPlaceholder("Type a command (for example: look)", { exact: true }).fill("look");
    await page.getByRole("button", { name: "Send", exact: true }).click();
    await page.getByText(expectedText, { exact: false }).first().waitFor({ state: "visible", timeout: 15000 });
    await page.screenshot({ path: path.join(evidenceDirectory, "portal.png"), fullPage: true });
    const evidence = {
      uri, browser: await browser.version(), sessionId: session.session.sessionId,
      gameId: session.session.gameId, gameKey: session.session.gameKey, expectedText, pageErrors: failures
    };
    await fs.writeFile(path.join(evidenceDirectory, "session.json"), JSON.stringify(evidence, null, 2));
    if (failures.length) throw new Error(`Portal errors: ${failures.join("; ")}`);
    process.stdout.write(JSON.stringify(evidence));
  } catch (error) {
    await fs.writeFile(path.join(evidenceDirectory, "failure.txt"), `${error.stack}\n${failures.join("\n")}\n${await page.locator("body").innerText()}`);
    throw error;
  } finally {
    await browser.close();
  }
})().catch(error => { process.stderr.write(error.stack || String(error)); process.exitCode = 1; });

import { expect, test } from "@playwright/test";

const SETUP_NAME = "DocuLensLocal-win-Setup.exe";
const LATEST = "https://github.com/boam79/DocuLensLocal/releases/latest";

test.describe("설치 유저스토리", () => {
  test("릴리스 Assets 목록이 스피너에서 벗어나 Setup.exe가 보인다", async ({
    page,
  }, testInfo) => {
    await page.goto(LATEST, { waitUntil: "domcontentloaded" });
    await testInfo.attach("url", { body: Buffer.from(page.url()) });

    const assets = page.getByText(/Assets\s+\d+/i).first();
    await expect(assets).toBeVisible({ timeout: 15_000 });

    const spinner = page.locator("[aria-label='Loading'], .anim-pulse, .circle-spin");
    const started = Date.now();
    await page.waitForFunction(
      () => {
        const text = document.body.innerText;
        return text.includes("DocuLensLocal-win-Setup.exe");
      },
      null,
      { timeout: 45_000 },
    );
    const waitedMs = Date.now() - started;
    await page.screenshot({
      path: testInfo.outputPath("assets-loaded.png"),
      fullPage: true,
    });
    await testInfo.attach("waited-ms", {
      body: Buffer.from(String(waitedMs)),
    });

    const setupLink = page.getByRole("link", { name: SETUP_NAME }).first();
    await expect(setupLink).toBeVisible();
    await expect(spinner).toHaveCount(0);

    const downloadPromise = page.waitForEvent("download", { timeout: 60_000 });
    await setupLink.click();
    const download = await downloadPromise;
    expect(download.suggestedFilename()).toBe(SETUP_NAME);
    const filePath = testInfo.outputPath(SETUP_NAME);
    await download.saveAs(filePath);
    const failure = await download.failure();
    expect(failure, `download failed: ${failure}`).toBeNull();
  });
});

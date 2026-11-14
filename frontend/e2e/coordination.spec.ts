import { test, expect } from '@playwright/test'
import type { Page } from '@playwright/test'
import { mkdir } from 'node:fs/promises'
import path from 'node:path'

async function signIn(page: Page, key: string) {
  await page.goto('/')
  await page.getByLabel('Email', { exact: true }).fill(process.env[`E2E_${key}_EMAIL`]!)
  await page.getByLabel('Password', { exact: true }).fill(process.env[`E2E_${key}_PASSWORD`]!)
  const response = page.waitForResponse(r => r.url().endsWith('/api/auth/login') && r.request().method() === 'POST')
  await page.getByRole('button', { name: 'Sign in', exact: true }).click()
  const result = await response
  expect(result.status()).toBe(200)
  await expect(page.getByLabel('Open user menu')).toBeVisible()
  return await result.json() as { accessToken: string; user: { storeId: number } }
}

test('store confirms time and contact, declined/changed times block repair, both roles can start confirmed repair', async ({ browser }) => {
  const admin = await browser.newPage()
  const manager = await browser.newPage()
  try {
    const a = await signIn(admin, 'ADMIN')
    const m = await signIn(manager, 'MANAGER1')
    const headers = { Authorization: `Bearer ${a.accessToken}` }
    const categories = await (await admin.request.get('/api/categories', { headers })).json()
    const created = await admin.request.post('/api/tickets', { headers, data: {
      title: 'Coordinated technician visit', description: 'Verify store confirmation before repair',
      categoryId: categories[0].id, storeId: m.user.storeId, priority: 3,
    } })
    expect(created.status()).toBe(201)
    const { id } = await created.json()
    const ticketPath = `/tickets/${id}`
    const apiPath = `/api${ticketPath}`
    const ticket = await (await admin.request.get(apiPath, { headers })).json()
    expect((await admin.request.post(`${apiPath}/approve`, { headers, data: { expectedVersion: ticket.version } })).status()).toBe(204)
    const approved = await (await admin.request.get(apiPath, { headers })).json()
    const technicians = await (await admin.request.get('/api/external-technicians', { headers })).json()
    expect((await admin.request.post(`${apiPath}/assign`, { headers, data: {
      expectedVersion: approved.version, externalTechnicianId: technicians[0].id,
      scheduledAt: '2099-01-01T10:00:00Z', notes: 'Meet store contact',
    } })).status()).toBe(204)
    for (const page of [admin, manager]) {
      await page.getByRole('link', { name: 'Tickets', exact: true }).click()
      await page.getByRole('link', { name: /Coordinated technician visit/ }).click()
    }
    await expect(manager.getByText('Awaiting store confirmation', { exact: true })).toBeVisible()
    await expect(admin.getByRole('button', { name: 'Confirm visit', exact: true })).toHaveCount(0)
    await expect(manager.getByRole('button', { name: 'Change visit time', exact: true })).toHaveCount(0)
    await expect(admin.getByRole('button', { name: 'Start repair', exact: true })).toHaveCount(0)
    await manager.getByRole('button', { name: 'Decline visit time', exact: true }).click()
    await manager.getByRole('button', { name: 'Confirm', exact: true }).click()
    await expect(manager.getByRole('alert')).toContainText('Enter a reason')
    await manager.getByLabel('Reason', { exact: true }).fill('Store is closed at that time.')
    await manager.getByRole('button', { name: 'Confirm', exact: true }).click()
    await expect(manager.getByText('Time declined', { exact: true })).toBeVisible()
    await expect(manager.locator('.ticket-status-line .status-tag')).toHaveText('Visit time declined')
    await admin.getByRole('link', { name: 'Overview', exact: true }).click()
    const attention = admin.locator('.attention-list li').filter({ hasText: 'Coordinated technician visit' })
    await expect(attention).toContainText('Change visit time')
    await expect(attention).toContainText('Visit time declined')
    await admin.getByRole('link', { name: 'Tickets', exact: true }).click()
    const row = admin.locator('tr').filter({ hasText: 'Coordinated technician visit' })
    await expect(row).toContainText('Visit time declined')
    await admin.getByRole('link', { name: /View ticket #.*Coordinated technician visit/ }).click()
    await expect(manager.getByRole('button', { name: 'Start repair', exact: true })).toHaveCount(0)
    await admin.getByRole('button', { name: 'Refresh details', exact: true }).click()
    await expect(admin.getByText('Store is closed at that time.', { exact: true })).toBeVisible()
    async function reschedule(value: string) {
      await admin.getByRole('button', { name: 'Change visit time', exact: true }).click()
      await admin.getByLabel('Visit time (your local time)').fill(value)
      await admin.getByRole('button', { name: 'Confirm', exact: true }).click()
      await expect(admin.getByText('Awaiting store confirmation', { exact: true })).toBeVisible()
      await expect(admin.locator('.ticket-status-line .status-tag')).toHaveText('Awaiting visit confirmation')
    }
    await reschedule('2099-01-02T12:00')
    await manager.getByRole('button', { name: 'Refresh details', exact: true }).click()
    await manager.getByRole('button', { name: 'Confirm visit', exact: true }).click()
    await manager.getByRole('button', { name: 'Confirm', exact: true }).click()
    await expect(manager.getByRole('alert')).toContainText('Enter a contact name')
    await manager.getByLabel('Contact name', { exact: true }).fill('On-site store contact')
    await manager.getByLabel('Contact phone', { exact: true }).fill('+973 3300 1234')
    await manager.route(`**${apiPath}/visit/confirm`, route => route.fulfill({ status: 409, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Refresh before trying again.' }) }))
    await manager.getByRole('button', { name: 'Confirm', exact: true }).click()
    await expect(manager.getByRole('alert')).toContainText('Refresh before trying again.')
    await expect(manager.getByLabel('Contact name', { exact: true })).toHaveValue('On-site store contact')
    await manager.unroute(`**${apiPath}/visit/confirm`)
    await manager.getByRole('button', { name: 'Confirm', exact: true }).click()
    await expect(manager.locator('section[aria-labelledby="assignment-title"]').getByText('Visit confirmed', { exact: true })).toBeVisible()
    await admin.getByRole('button', { name: 'Refresh details', exact: true }).click()
    await expect(admin.getByText(/On-site store contact ·/)).toBeVisible()
    await reschedule('2099-01-03T12:00')
    // The manager still sees an earlier confirmed snapshot, but cannot start repair.
    await manager.getByRole('button', { name: 'Start repair', exact: true }).click()
    await manager.getByRole('button', { name: 'Confirm', exact: true }).click()
    await expect(manager.getByRole('alert')).toContainText(/changed|refresh/i)
    await manager.getByRole('button', { name: 'Cancel', exact: true }).click()
    await manager.getByRole('button', { name: 'Refresh details', exact: true }).click()
    await manager.getByRole('button', { name: 'Confirm visit', exact: true }).click()
    await manager.getByLabel('Contact name', { exact: true }).fill('New store contact')
    await manager.getByLabel('Contact phone', { exact: true }).fill('+973 3300 5678')
    await manager.getByRole('button', { name: 'Confirm', exact: true }).click()
    await expect(manager.locator('section[aria-labelledby="assignment-title"]').getByText('Visit confirmed', { exact: true })).toBeVisible()
    const screenshots = process.env.E2E_SCREENSHOT_DIR ?? '/tmp/servicedesk-visit-screenshots'
    await mkdir(screenshots, { recursive: true })
    await manager.screenshot({ path: path.join(screenshots, 'visit-confirmed-desktop.png'), fullPage: true })
    await manager.setViewportSize({ width: 390, height: 844 })
    expect(await manager.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
    await manager.screenshot({ path: path.join(screenshots, 'visit-confirmed-mobile.png'), fullPage: true })
    await admin.getByRole('button', { name: 'Refresh details', exact: true }).click()
    await admin.getByRole('button', { name: 'Start repair', exact: true }).click()
    await admin.getByRole('button', { name: 'Confirm', exact: true }).click()
    await expect(admin.locator('.ticket-status-line .status-tag').first()).toHaveText('In progress')
    await expect(admin.getByRole('button', { name: 'Change visit time', exact: true })).toHaveCount(0)
  } finally { await admin.close(); await manager.close() }
})

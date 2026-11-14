import { test, expect } from '@playwright/test'
import type { Page } from '@playwright/test'
import path from 'node:path'
import { mkdir } from 'node:fs/promises'

const screenshots = process.env.E2E_SCREENSHOT_DIR ?? path.resolve(
  import.meta.dirname,
  '../../docs/screenshots/redesign',
)
const version = 'AAAAAAAAAAA='
const tickets = [
  {
    id: 142,
    title: 'Cold room temperature rising above safe range',
    description:
      'The cold room is reading 9°C. Please inspect the refrigeration unit and door seals.',
    status: 1,
    priority: 4,
    storeId: 1,
    storeName: 'Seef District',
    createdAt: '2026-11-12T08:30:00Z',
    version,
    categoryId: 1,
    categoryName: 'Refrigeration',
    createdByUserId: 2,
    createdByName: 'Maya Hassan',
    assetId: null,
  },
  {
    id: 141,
    title: 'Entrance lighting needs replacement',
    description: 'Three ceiling lights near the entrance are flickering.',
    status: 2,
    priority: 2,
    storeId: 2,
    storeName: 'City Centre',
    createdAt: '2026-11-11T10:00:00Z',
    version,
    categoryId: 1,
    categoryName: 'Electrical',
    createdByUserId: 2,
    createdByName: 'Maya Hassan',
    assetId: null,
  },
  {
    id: 140,
    title: 'Checkout printer losing connection',
    description:
      'The receipt printer disconnects intermittently during peak hours.',
    status: 4,
    priority: 3,
    storeId: 1,
    storeName: 'Seef District',
    createdAt: '2026-11-10T10:00:00Z',
    version,
    categoryId: 1,
    categoryName: 'Equipment',
    createdByUserId: 2,
    createdByName: 'Maya Hassan',
    assetId: null,
  },
]
async function fixture(page: Page, manager = false) {
  let profile = {
    id: manager ? 2 : 1,
    firstName: manager ? 'Maya' : 'Amal',
    lastName: 'Hassan',
    email: manager ? 'maya@example.test' : 'amal@example.test',
    phoneNumber: '+973 3300 1234',
    role: manager ? 'StoreManager' : 'Admin',
    storeId: manager ? 1 : null,
    storeName: manager ? 'Seef District' : null,
    version,
  }
  const stores = [
    {
      id: 1,
      name: 'Seef District',
      storeCode: 'SEEF-01',
      address: 'Building 12, Road 2803',
      city: 'Manama',
      isActive: true,
      version,
    },
    {
      id: 2,
      name: 'City Centre',
      storeCode: 'CITY-02',
      address: 'Bahrain City Centre',
      city: 'Manama',
      isActive: false,
      version,
    },
  ]
  let comments = [
    {
      id: 1,
      supportTicketId: 142,
      authorId: 2,
      authorName: 'Maya Hassan',
      text: 'The store team has moved stock to the backup cold room while we wait for inspection.',
      createdAt: '2026-11-12T09:00:00Z',
    },
  ]
  await page.route('**/api/**', async (route) => {
    const url = new URL(route.request().url())
    const p = url.pathname
    if (!p.startsWith('/api/')) return route.fallback()
    const method = route.request().method()
    const json = (data: unknown, status = 200) =>
      route.fulfill({
        status,
        contentType: 'application/json',
        body: JSON.stringify(data),
      })
    if (p === '/api/auth/login')
      return json({
        accessToken: 'browser-fixture',
        tokenType: 'Bearer',
        expiresAt: new Date(Date.now() + 1800000).toISOString(),
        user: {
          id: profile.id,
          name: `${profile.firstName} ${profile.lastName}`,
          role: profile.role,
          storeId: profile.storeId,
          storeName: profile.storeName,
        },
      })
    if (p === '/api/dashboard')
      return json({
        summary: { total: 24, open: 12, inProgress: 7, resolved: 5 },
        recent: tickets,
        attention: manager
          ? [{ ticket: tickets[2], action: 'Update progress' }]
          : [
              { ticket: tickets[0], action: 'Review request' },
              { ticket: tickets[1], action: 'Assign technician' },
            ],
      })
    if (p === '/api/account') return json(profile)
    if (p === '/api/account/profile') {
      const input = route.request().postDataJSON()
      profile = {
        ...profile,
        firstName: input.firstName,
        lastName: input.lastName,
        phoneNumber: input.phoneNumber,
      }
      return json(profile)
    }
    if (p === '/api/notifications')
      return json({
        items: [
          {
            id: 1,
            supportTicketId: 142,
            kind: 'Comment added',
            createdAt: '2026-11-12T09:00:00Z',
            readAt: null,
          },
        ],
        unreadCount: 1,
        nextBeforeId: null,
      })
    if (p.endsWith('/read')) return route.fulfill({ status: 204 })
    if (p === '/api/admin/stores' || p === '/api/stores') return json(stores)
    if (p === '/api/categories')
      return json([{ id: 1, name: 'Equipment maintenance' }])
    if (p === '/api/admin/store-managers')
      return json([
        {
          id: 2,
          firstName: 'Maya',
          lastName: 'Hassan',
          email: 'maya@example.test',
          phoneNumber: '+973 3300 1234',
          storeId: 1,
          storeName: 'Seef District',
          isActive: true,
          version,
        },
      ])
    if (p === '/api/admin/external-technicians')
      return json([
        {
          id: 1,
          fullName: 'Omar Khalid',
          companyName: 'Precision Maintenance',
          phoneNumber: '+973 3600 5678',
          specialisation: 'Refrigeration & electrical',
          isActive: true,
          version,
        },
      ])
    if (p === '/api/tickets' && method === 'POST')
      return json(
        { ...tickets[0], id: 143, ...route.request().postDataJSON() },
        201,
      )
    if (p === '/api/tickets') {
      const title = url.searchParams.get('title')?.toLowerCase() || ''
      const items = tickets.filter(
        (t) =>
          t.title.toLowerCase().includes(title) &&
          (!url.searchParams.has('status') ||
            t.status === Number(url.searchParams.get('status'))),
      )
      return json({ items, totalCount: items.length, page: 1, pageSize: 25 })
    }
    if (p.endsWith('/history'))
      return json([
        {
          id: 1,
          supportTicketId: 142,
          previousStatus: null,
          newStatus: 1,
          changedByUserId: 2,
          changedByName: 'Maya Hassan',
          reason: 'Maintenance request submitted.',
          changedAt: tickets[0].createdAt,
        },
      ])
    if (p.endsWith('/attachments'))
      return json({ items: [], uploadsEnabled: true })
    if (p.endsWith('/comments')) {
      if (method === 'POST') {
        comments = [
          ...comments,
          {
            ...comments[0],
            id: 2,
            authorName: 'Amal Hassan',
            text: route.request().postDataJSON().text,
          },
        ]
        return route.fulfill({ status: 201 })
      }
      return json({ items: comments, nextBeforeId: null })
    }
    if (/\/api\/tickets\/\d+$/.test(p))
      return json(
        tickets.find((t) => t.id === Number(p.split('/').at(-1))) || tickets[0],
      )
    return json({ detail: 'Fixture endpoint unavailable' }, 404)
  })
}
async function signIn(page: Page, entry = '/') {
  await page.goto(entry)
  await page.getByLabel('Email', { exact: true }).fill('review@example.test')
  await page.getByLabel('Password', { exact: true }).fill('Fixture-password8!')
  await page.getByRole('button', { name: 'Sign in', exact: true }).click()
  await expect(
    page.getByRole('heading', { name: 'Needs attention' }),
  ).toBeVisible()
}
async function navigate(page: Page, name: string) {
  if (page.viewportSize()!.width <= 760)
    await page.getByRole('button', { name: 'Open navigation' }).click()
  await page
    .getByRole('link', { name, exact: true })
    .filter({ visible: true })
    .click()
}
async function account(page: Page) {
  await page.getByLabel('Open user menu').click()
  await page.getByRole('link', { name: 'Account settings' }).click()
}
async function screenshot(page: Page, name: string) {
  await mkdir(screenshots, { recursive: true })
  for (const select of await page.locator('select:visible').all()) {
    await expect(select).toHaveCSS('appearance', 'none')
    await expect(select).toHaveCSS('padding-right', '40px')
    await expect(select).toHaveCSS('background-position', 'calc(100% - 12px) 50%')
  }
  await page.screenshot({
    path: path.join(screenshots, `${name}.png`),
    fullPage: true,
    animations: 'disabled',
  })
}
async function noOverflow(page: Page) {
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth + 1,
    ),
  ).toBe(true)
}

for (const width of [1440, 768, 390])
  test(`every redesigned page is usable at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 1000 })
    await fixture(page)
    const errors: string[] = []
    page.on('pageerror', (error) => {
      errors.push(error.message)
      console.error('Browser error:', error.message)
    })
    await page.goto('/')
    await expect(
      page.getByRole('heading', { name: 'Sign in to your workspace' }),
    ).toBeVisible()
    await screenshot(page, `login-${width}`)
    await noOverflow(page)
    await signIn(page)
    await screenshot(page, `overview-${width}`)
    await noOverflow(page)
    await navigate(page, 'Tickets')
    await expect(
      page.getByRole('heading', { name: 'All tickets' }),
    ).toBeVisible()
    await screenshot(page, `tickets-${width}`)
    await noOverflow(page)
    await page.getByRole('button', { name: 'New ticket', exact: true }).click()
    await expect(page.getByRole('dialog')).toBeVisible()
    await screenshot(page, `create-ticket-${width}`)
    await noOverflow(page)
    await page.getByRole('button', { name: 'Close form' }).click()
    await page
      .getByRole('link', { name: /View ticket #142/ })
      .filter({ visible: true })
      .click()
    await expect(
      page.getByRole('heading', { name: tickets[0].title }),
    ).toBeVisible()
    await screenshot(page, `ticket-details-${width}`)
    await noOverflow(page)
    await page.getByRole('tab', { name: 'History' }).click()
    await expect(
      page.getByRole('heading', { name: 'Status history' }),
    ).toBeVisible()
    await screenshot(page, `history-${width}`)
    for (const name of ['Stores', 'Store managers', 'Technicians']) {
      await navigate(page, name)
      await expect(
        page.getByRole('heading', { name, exact: true }),
      ).toBeVisible()
      await expect(page.getByText('Loading…')).toHaveCount(0)
      await expect(page.locator('.skeleton-row')).toHaveCount(0)
      await screenshot(page, `${name.toLowerCase().replace(' ', '-')}-${width}`)
      await noOverflow(page)
    }
    await account(page)
    await expect(page.getByLabel('First name')).toHaveValue('Amal')
    await screenshot(page, `account-${width}`)
    await noOverflow(page)
    await page.getByRole('button', { name: 'Security', exact: true }).click()
    await screenshot(page, `security-${width}`)
    await noOverflow(page)
    await page.getByLabel('Notifications, 1 unread').click()
    await expect(
      page.getByRole('link', { name: /Comment added/ }),
    ).toBeVisible()
    await screenshot(page, `notifications-${width}`)
    expect(errors).toEqual([])
  })
test('profile persists, forms protect drafts, filters restore and comment posting preserves context', async ({
  page,
}) => {
  await fixture(page)
  await signIn(page)
  await account(page)
  await page.getByLabel('First name').fill('Amina')
  await page.getByRole('button', { name: 'Save changes', exact: true }).click()
  await expect(page.getByRole('status')).toContainText('updated')
  await expect(page.getByLabel('Open user menu')).toContainText('Amina Hassan')
  await navigate(page, 'Tickets')
  await page.getByLabel('Search title').fill('cold room')
  await expect(page).toHaveURL(/title=cold/)
  await expect(
    page.getByRole('heading', { name: 'Filtered tickets' }),
  ).toBeVisible()
  await page.getByRole('button', { name: 'New ticket', exact: true }).click()
  await page.getByRole('dialog').getByLabel('Title').fill('Draft repair')
  page.once('dialog', (d) => d.dismiss())
  await page.keyboard.press('Escape')
  await expect(page.getByRole('dialog')).toBeVisible()
  page.once('dialog', (d) => d.accept())
  await page.keyboard.press('Escape')
  await expect(page.getByRole('dialog')).not.toBeVisible()
  await page
    .getByRole('link', { name: /View ticket #142/ })
    .filter({ visible: true })
    .click()
  await page
    .getByLabel('Add a comment', { exact: true })
    .fill('Technician has been contacted.')
  await page.getByRole('button', { name: 'Post comment' }).click()
  await expect(
    page.getByText('Technician has been contacted.', { exact: true }),
  ).toBeVisible()
  await expect(page.getByRole('tab', { name: 'Discussion' })).toHaveAttribute(
    'aria-selected',
    'true',
  )
  await page.getByRole('tab', { name: 'Discussion' }).focus()
  await page.keyboard.press('ArrowRight')
  await expect(page.getByRole('tab', { name: 'Photos' })).toBeFocused()
  await page.goBack()
  await expect(page.getByLabel('Search title')).toHaveValue('cold room')
  await account(page)
  await expect(page.getByLabel('First name')).toHaveValue('Amina')
})
test('manager navigation and request form reflect assigned store on mobile', async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 })
  await fixture(page, true)
  await signIn(page)
  await navigate(page, 'Tickets')
  await expect(
    page.getByRole('link', { name: 'Stores', exact: true }),
  ).toHaveCount(0)
  await page.getByRole('button', { name: 'New ticket', exact: true }).click()
  await expect(page.getByRole('dialog').getByLabel('Store')).toHaveValue('Seef District')
  await expect(page.getByRole('dialog').getByLabel('Store')).toHaveAttribute(
    'readonly',
    '',
  )
  await page.getByRole('dialog').getByLabel('Title').fill('Manager request')
  await page
    .getByRole('dialog').getByLabel('Description')
    .fill('An equipment repair is needed at our store.')
  await page.getByRole('dialog').getByLabel('Category').selectOption('1')
  await page.getByRole('button', { name: 'Create ticket', exact: true }).click()
  await expect(page.getByRole('status')).toContainText('created successfully')
  await noOverflow(page)
})

for (const width of [1440, 390])
  test(`status correction uses shared typography at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 1000 })
    await fixture(page)
    await page.route('**/api/tickets/142', route => route.fulfill({
      json: { ...tickets[0], status: 5 },
    }))
    await page.route('**/api/tickets/142/history', route => route.fulfill({
      json: [{
        id: 11, supportTicketId: 142, previousStatus: 4, newStatus: 5,
        changedByUserId: 2, changedByName: 'Maya Hassan',
        reason: 'Repair completed.', changedAt: tickets[0].createdAt,
      }],
    }))
    await signIn(page)
    await page.locator('.attention-list a[href="/tickets/142"]').click()
    await page.getByText('Administrator actions', { exact: true }).click()
    await page.getByRole('button', { name: 'Correct status', exact: true }).click()
    const note = page.getByText(/Return Resolved to In progress by reversing event #11/)
    await expect(note).toBeVisible()
    await expect(note).toHaveCSS('font-size', '13px')
    const label = page.locator('label[for="correction-reason"]')
    await expect(label).toHaveCSS('font-size', '13px')
    await expect(label).toHaveCSS('font-weight', '600')
    await expect(page.getByLabel('Correction reason')).toHaveCSS('font-size', '14px')
    await page.getByLabel('Correction reason').fill('Repair needs another check.')
    await expect(page.getByLabel('Correction reason')).toHaveValue('Repair needs another check.')
    await noOverflow(page)
    await page.screenshot({ path: `/tmp/servicedesk-correction-${width}.png`, fullPage: true })
    await page.getByRole('button', { name: 'Cancel', exact: true }).click()
    await expect(page.getByRole('button', { name: 'Correct status', exact: true })).toBeVisible()
  })

for (const manager of [false, true])
  test(`${manager ? 'manager' : 'admin'} sign-in always opens overview`, async ({ page }) => {
    await fixture(page, manager)
    await signIn(page, '/tickets?status=4#create-ticket')
    await expect(page).toHaveURL('http://127.0.0.1:5174/')
    await navigate(page, 'Tickets')
    await page.getByLabel('Open user menu').click()
    await page.getByRole('button', { name: 'Sign out', exact: true }).click()
    await expect(page.getByRole('heading', { name: 'Sign in to your workspace' })).toBeVisible()
    await page.getByLabel('Email', { exact: true }).fill('review@example.test')
    await page.getByLabel('Password', { exact: true }).fill('Fixture-password8!')
    await page.getByRole('button', { name: 'Sign in', exact: true }).click()
    await expect(page).toHaveURL('http://127.0.0.1:5174/')
    await expect(page.getByRole('heading', { name: 'Needs attention' })).toBeVisible()
  })

test('discussion reads oldest to newest, loads earlier comments above and posts at the bottom', async ({ page }) => {
  await fixture(page)
  const comments = [1, 2, 3, 4].map(id => ({
    id, supportTicketId: 142, authorId: 2, authorName: 'Maya Hassan',
    text: `Repair update ${id}`, createdAt: '2026-11-12T09:00:00Z',
  }))
  await page.route('**/api/tickets/142/comments*', async route => {
    if (route.request().method() === 'POST') {
      comments.push({ ...comments[0], id: 5, text: route.request().postDataJSON().text })
      return route.fulfill({ status: 201 })
    }
    const beforeId = Number(new URL(route.request().url()).searchParams.get('beforeId')) || Infinity
    const remaining = comments.filter(c => c.id < beforeId).sort((a, b) => b.id - a.id)
    const items = remaining.slice(0, 2)
    return route.fulfill({ json: {
      items, nextBeforeId: remaining.length > 2 ? items.at(-1)!.id : null,
    } })
  })
  await signIn(page)
  await page.locator('.attention-list a[href="/tickets/142"]').click()
  const messages = page.locator('.comment-list .full-description')
  await expect(messages).toHaveText(['Repair update 3', 'Repair update 4'])
  const older = page.getByRole('button', { name: 'Load older comments' })
  expect(await older.evaluate(button => Boolean(button.compareDocumentPosition(document.querySelector('.comment-list')!) & Node.DOCUMENT_POSITION_FOLLOWING))).toBe(true)
  await older.click()
  await expect(messages).toHaveText(['Repair update 1', 'Repair update 2', 'Repair update 3', 'Repair update 4'])
  await expect(older).toHaveCount(0)
  await page.getByLabel('Add a comment', { exact: true }).fill('Latest repair update')
  await page.getByRole('button', { name: 'Post comment', exact: true }).click()
  await expect(messages).toHaveText(['Repair update 4', 'Latest repair update'])
  await expect(page.getByLabel('Add a comment', { exact: true })).toHaveValue('')
  await page.getByRole('button', { name: 'Refresh discussion' }).click()
  await expect(messages).toHaveText(['Repair update 4', 'Latest repair update'])
})

test('discussion shows only the latest five until older comments are requested', async ({ page }) => {
  await fixture(page)
  const comments = Array.from({ length: 12 }, (_, index) => ({
    id: index + 1, supportTicketId: 142, authorId: 2, authorName: 'Maya Hassan',
    text: `Repair update ${index + 1}`, createdAt: '2026-11-12T09:00:00Z',
  }))
  await page.route('**/api/tickets/142/comments*', async route => {
    if (route.request().method() === 'POST') {
      comments.push({ ...comments[0], id: 13, text: route.request().postDataJSON().text })
      return route.fulfill({ status: 201 })
    }
    return route.fulfill({ json: { items: [...comments].reverse(), nextBeforeId: null } })
  })
  await signIn(page)
  await page.locator('.attention-list a[href="/tickets/142"]').click()
  const messages = page.locator('.comment-list .full-description')
  await expect(messages).toHaveText(comments.slice(-5).map(c => c.text))
  const older = page.getByRole('button', { name: 'Load older comments' })
  await older.click()
  await expect(messages).toHaveText(comments.slice(-10).map(c => c.text))
  await older.click()
  await expect(messages).toHaveText(comments.map(c => c.text))
  await expect(older).toHaveCount(0)
  await page.getByLabel('Add a comment', { exact: true }).fill('Latest repair update')
  await page.getByRole('button', { name: 'Post comment', exact: true }).click()
  await expect(messages).toHaveText(['Repair update 9', 'Repair update 10', 'Repair update 11', 'Repair update 12', 'Latest repair update'])
  await page.getByRole('button', { name: 'Refresh discussion' }).click()
  await expect(messages).toHaveCount(5)
  await expect(messages.last()).toHaveText('Latest repair update')
  await expect(older).toBeVisible()
})

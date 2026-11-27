// SPDX-License-Identifier: LGPL-3.0-or-later
import { expect, test } from '@playwright/test'

test('a ticket raised in the browser shows up on top of the list', async ({ page }) => {
  const title = `No imprime la impresora ${Date.now()}`

  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'Ticket' })).toBeVisible()
  await page.locator('a[href="#/itdesk.ticket/new"]').first().click()

  await page.getByLabel('Título').fill(title)
  await page.getByLabel('Descripción').fill('Desde ayer no sale nada por la bandeja 2.')
  await page.getByRole('button', { name: 'Guardar' }).click()

  const firstRow = page.getByRole('row').nth(1)
  await expect(firstRow).toContainText(title)
  await expect(firstRow).toContainText('Normal')
  await expect(firstRow.locator('time')).toBeVisible()

  await firstRow.getByRole('link', { name: title }).click()
  await expect(page.getByText('Desde ayer no sale nada por la bandeja 2.')).toBeVisible()
})

test('a ticket without a title is not saved and the title is marked as required', async ({ page }) => {
  await page.goto('/#/itdesk.ticket/new')

  await page.getByRole('button', { name: 'Guardar' }).click()

  await expect(page.getByLabel('Título')).toHaveAccessibleDescription('Este dato es obligatorio.')
  await expect(page).toHaveURL(/#\/itdesk\.ticket\/new$/)
})

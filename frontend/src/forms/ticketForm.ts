import type { ReferenceItem } from '../api/referenceData'

export type TicketFormValues = {
  title: string
  description: string
  storeId: string
  categoryId: string
  priority: string
}

export type TicketFormErrors = Partial<Record<keyof TicketFormValues, string>>

export type EditTicketValues = Pick<TicketFormValues, 'title' | 'description' | 'priority'>

export function validateTicketDetails(values: EditTicketValues): TicketFormErrors {
  const errors: TicketFormErrors = {}
  if (!values.title.trim()) errors.title = 'Enter a title.'
  else if (values.title.trim().length > 200) errors.title = 'Use 200 characters or fewer.'
  if (!values.description.trim()) errors.description = 'Describe the issue.'
  else if (values.description.trim().length > 4000) errors.description = 'Use 4,000 characters or fewer.'
  if (!['1', '2', '3', '4'].includes(values.priority)) errors.priority = 'Select a valid priority.'
  return errors
}

export function validateTicketForm(
  values: TicketFormValues,
  stores: ReferenceItem[],
  categories: ReferenceItem[],
): TicketFormErrors {
  const errors = validateTicketDetails(values)
  if (!stores.some((store) => String(store.id) === values.storeId)) errors.storeId = 'Select an available store.'
  if (!categories.some((category) => String(category.id) === values.categoryId)) errors.categoryId = 'Select an available category.'
  return errors
}

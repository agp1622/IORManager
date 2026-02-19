export const DEFAULT_NCF_CATEGORY = 'B02'

export const NCF_CATEGORY_OPTIONS = [
  { code: 'B01', sequenceLength: 8 },
  { code: 'B02', sequenceLength: 8 },
  { code: 'B03', sequenceLength: 8 },
  { code: 'B04', sequenceLength: 8 },
  { code: 'B11', sequenceLength: 8 },
  { code: 'B12', sequenceLength: 8 },
  { code: 'B13', sequenceLength: 8 },
  { code: 'B14', sequenceLength: 8 },
  { code: 'B15', sequenceLength: 8 },
  { code: 'B16', sequenceLength: 8 },
  { code: 'B17', sequenceLength: 8 },
  { code: 'E31', sequenceLength: 10 },
  { code: 'E32', sequenceLength: 10 },
  { code: 'E33', sequenceLength: 10 },
  { code: 'E34', sequenceLength: 10 },
  { code: 'E41', sequenceLength: 10 },
  { code: 'E43', sequenceLength: 10 },
  { code: 'E44', sequenceLength: 10 },
  { code: 'E45', sequenceLength: 10 },
  { code: 'E46', sequenceLength: 10 },
  { code: 'E47', sequenceLength: 10 },
]

const getNormalizedOptions = (categories) =>
  Array.isArray(categories) && categories.length > 0 ? categories : NCF_CATEGORY_OPTIONS

export const normalizeNcfCategory = (value, categories = NCF_CATEGORY_OPTIONS) => {
  if (!value || typeof value !== 'string') {
    return null
  }

  const options = getNormalizedOptions(categories)
  const normalized = value.trim().toUpperCase()
  return options.some((option) => option.code === normalized) ? normalized : null
}

export const inferNcfCategoryFromNumber = (ncfNumber, categories = NCF_CATEGORY_OPTIONS) => {
  if (!ncfNumber || typeof ncfNumber !== 'string') {
    return null
  }

  const compact = ncfNumber.toUpperCase().replace(/[\s-]/g, '')
  if (compact.length < 4) {
    return null
  }

  const code = compact.slice(0, 3)
  const suffix = compact.slice(3)
  if (!/^\d+$/.test(suffix)) {
    return null
  }

  return normalizeNcfCategory(code, categories)
}

export const getNcfSequenceLength = (categoryCode, categories = NCF_CATEGORY_OPTIONS) => {
  const options = getNormalizedOptions(categories)
  const normalized = normalizeNcfCategory(categoryCode, options)
  if (!normalized) {
    return 8
  }

  return options.find((option) => option.code === normalized)?.sequenceLength ?? 8
}

export const getNcfPlaceholderForCategory = (
  categoryCode,
  categories = NCF_CATEGORY_OPTIONS,
) => {
  const options = getNormalizedOptions(categories)
  const normalized = normalizeNcfCategory(categoryCode, options) || DEFAULT_NCF_CATEGORY
  return `${normalized}${'0'.repeat(getNcfSequenceLength(normalized, options))}`
}

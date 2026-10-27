/**
 * API error representing either a backend HTTP error response (RFC 7807 ProblemDetails / ValidationProblemDetails)
 * or a client-side network failure.
 */
export class ApiError extends Error {
  constructor(message, { status = 0, isNetworkError = false, details = null, title = '' } = {}) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.isNetworkError = isNetworkError
    this.details = details
    this.title = title
  }
}

/**
 * Handles the HTTP response, parsing JSON ProblemDetails/ValidationProblemDetails or providing
 * a robust text fallback.
 */
async function handleResponse(response) {
  if (response.ok) {
    if (response.status === 204) {
      return null
    }
    return await response.json()
  }

  let errorMessage = ''
  let title = ''
  let parsedJson = null

  const contentType = response.headers.get('content-type') || ''
  if (contentType.includes('application/json') || contentType.includes('application/problem+json')) {
    try {
      parsedJson = await response.json()
      title = parsedJson.title || ''

      // Handle ValidationProblemDetails (errors map)
      if (parsedJson.errors && typeof parsedJson.errors === 'object') {
        const errorList = Object.entries(parsedJson.errors)
          .flatMap(([, messages]) => (Array.isArray(messages) ? messages : [messages]))
          .filter(Boolean)

        if (errorList.length > 0) {
          errorMessage = errorList.join(' ')
        }
      }

      // Handle ProblemDetails (detail string)
      if (!errorMessage && parsedJson.detail) {
        errorMessage = parsedJson.detail
      }

      if (!errorMessage && title) {
        errorMessage = title
      }
    } catch {
      // Fallback if JSON parsing fails despite header
    }
  }

  if (!errorMessage) {
    try {
      const text = await response.text()
      if (text && text.trim().length > 0) {
        errorMessage = text.length > 200 ? `${text.slice(0, 197)}...` : text
      }
    } catch {
      // Ignore text read failures
    }
  }

  if (!errorMessage) {
    errorMessage = `Request failed with status ${response.status} (${response.statusText || 'Error'})`
  }

  throw new ApiError(errorMessage, {
    status: response.status,
    isNetworkError: false,
    details: parsedJson,
    title,
  })
}

/**
 * Safe fetch wrapper that distinguishes client network failures from backend HTTP responses.
 */
async function safeFetch(url, options = {}) {
  try {
    const response = await fetch(url, options)
    return await handleResponse(response)
  } catch (err) {
    // Re-throw if already an ApiError (from handleResponse)
    if (err instanceof ApiError) {
      throw err
    }

    // Pass through AbortError so cancellable requests can be ignored silently by callers
    if (err.name === 'AbortError') {
      throw err
    }

    // Network / connectivity failure
    throw new ApiError('Unable to connect to the server. Please verify that the backend API is running.', {
      status: 0,
      isNetworkError: true,
      details: err,
    })
  }
}

/**
 * Retrieves all branches.
 */
export async function getBranches(signal) {
  return await safeFetch('/api/branches', { signal })
}

/**
 * Retrieves all products.
 */
export async function getProducts(signal) {
  return await safeFetch('/api/products', { signal })
}

/**
 * Retrieves the stock for a single branch.
 */
export async function getBranchStock(branchId, signal) {
  return await safeFetch(`/api/branches/${branchId}/stock`, { signal })
}

/**
 * Retrieves stock across all provided branches in parallel.
 * Returns an array of branch stock responses: [{ branchId, branchName, items: [...] }, ...]
 */
export async function getAllBranchStocks(branches, signal) {
  if (!branches || branches.length === 0) {
    return []
  }

  return await Promise.all(
    branches.map(async (b) => {
      return await getBranchStock(b.id, signal)
    })
  )
}

/**
 * Performs a stock transfer. Never automatically retries.
 */
export async function createTransfer(transferData) {
  return await safeFetch('/api/transfers', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(transferData),
  })
}

/**
 * Retrieves all completed transfers ordered newest first.
 */
export async function getTransfers(signal) {
  return await safeFetch('/api/transfers', { signal })
}

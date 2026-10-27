import { useState } from 'react'
import { createTransfer } from '../services/inventoryApi'

const MAX_QUANTITY = 2147483647

export default function TransferForm({
  branches,
  products,
  stockByBranch,
  onTransferSuccess,
}) {
  const [fromBranchId, setFromBranchId] = useState('')
  const [toBranchId, setToBranchId] = useState('')
  const [productId, setProductId] = useState('')
  const [quantity, setQuantity] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [validationErrors, setValidationErrors] = useState({})
  const [feedback, setFeedback] = useState(null)

  // Clear stale feedback and field errors when inputs change
  const handleFromBranchChange = (e) => {
    const newFrom = e.target.value
    setFromBranchId(newFrom)
    if (toBranchId === newFrom) {
      setToBranchId('')
    }
    setValidationErrors((prev) => ({ ...prev, fromBranchId: undefined, toBranchId: undefined }))
    setFeedback(null)
  }

  const handleToBranchChange = (e) => {
    setToBranchId(e.target.value)
    setValidationErrors((prev) => ({ ...prev, toBranchId: undefined }))
    setFeedback(null)
  }

  const handleProductChange = (e) => {
    setProductId(e.target.value)
    setValidationErrors((prev) => ({ ...prev, productId: undefined }))
    setFeedback(null)
  }

  // Accepts only an empty string or ASCII digits (/^[0-9]*$/). Rejects invalid edits.
  const handleQuantityChange = (e) => {
    const val = e.target.value
    if (val === '' || /^[0-9]*$/.test(val)) {
      setQuantity(val)
      setValidationErrors((prev) => ({ ...prev, quantity: undefined }))
      setFeedback(null)
    }
  }

  // Step up: empty starts at 1; otherwise increments by 1 up to MAX_QUANTITY
  const handleStepUp = () => {
    if (isSubmitting) return

    setValidationErrors((prev) => ({ ...prev, quantity: undefined }))
    setFeedback(null)

    if (!quantity) {
      setQuantity('1')
      return
    }

    const current = Number(quantity)
    if (Number.isSafeInteger(current) && current < MAX_QUANTITY) {
      setQuantity(String(current + 1))
    }
  }

  // Step down: decreases by 1; does nothing if empty or <= 1
  const handleStepDown = () => {
    if (isSubmitting || !quantity) return

    const current = Number(quantity)
    if (current <= 1) return

    setValidationErrors((prev) => ({ ...prev, quantity: undefined }))
    setFeedback(null)

    setQuantity(String(current - 1))
  }

  // Keyboard navigation: ArrowUp/ArrowDown when the input is focused
  const handleQuantityKeyDown = (e) => {
    if (e.key === 'ArrowUp') {
      e.preventDefault()
      handleStepUp()
    } else if (e.key === 'ArrowDown') {
      e.preventDefault()
      handleStepDown()
    }
  }

  const isQuantityEmpty = quantity === ''
  const numericQuantity = isQuantityEmpty ? 0 : Number(quantity)
  const isDecrementDisabled = isSubmitting || isQuantityEmpty || numericQuantity <= 1
  const isIncrementDisabled = isSubmitting || (!isQuantityEmpty && numericQuantity >= MAX_QUANTITY)

  // Determine availability hint for selected branch and product
  const selectedProduct = products.find((p) => p.id === Number(productId))
  let availabilityHint = null
  if (fromBranchId && productId && selectedProduct) {
    const availableCount = stockByBranch[Number(fromBranchId)]?.[Number(productId)]
    if (typeof availableCount === 'number') {
      availabilityHint = `Available: ${availableCount} packages · ${selectedProduct.packageSize} each`
    } else {
      availabilityHint = `Package size: ${selectedProduct.packageSize}`
    }
  }

  const handleSubmit = async (e) => {
    e.preventDefault()

    if (isSubmitting) {
      return
    }

    // Client-side validation
    const errors = {}

    if (!fromBranchId) {
      errors.fromBranchId = 'Please select a source branch.'
    }

    if (!toBranchId) {
      errors.toBranchId = 'Please select a destination branch.'
    } else if (toBranchId === fromBranchId) {
      errors.toBranchId = 'Source and destination branches cannot be the same.'
    }

    if (!productId) {
      errors.productId = 'Please select a product.'
    }

    const trimmedQuantity = quantity.trim()
    if (!trimmedQuantity) {
      errors.quantity = 'Please enter a transfer quantity.'
    } else if (!/^[1-9]\d*$/.test(trimmedQuantity)) {
      errors.quantity = 'Quantity must be a positive whole number greater than zero.'
    } else {
      const num = Number(trimmedQuantity)
      if (!Number.isSafeInteger(num) || num > MAX_QUANTITY) {
        errors.quantity = 'Quantity exceeds the maximum allowed limit (2,147,483,647).'
      }
    }

    if (Object.keys(errors).length > 0) {
      setValidationErrors(errors)
      return
    }

    setValidationErrors({})
    setIsSubmitting(true)
    setFeedback(null)

    const payload = {
      fromBranchId: Number(fromBranchId),
      toBranchId: Number(toBranchId),
      productId: Number(productId),
      quantity: Number(trimmedQuantity),
    }

    try {
      const receipt = await createTransfer(payload)

      // Clear quantity on success, preserve branch and product selections
      setQuantity('')

      const successMessage = `Transferred ${receipt.quantity} packages of ${receipt.productName} from ${receipt.fromBranchName} to ${receipt.toBranchName}.`

      let refreshWarning = null
      try {
        await onTransferSuccess(receipt)
      } catch {
        refreshWarning =
          'Transfer was processed successfully on the server, but the updated stock table could not be refreshed. Please click "Refresh" above.'
      }

      setFeedback({
        type: 'success',
        message: successMessage,
        refreshWarning,
      })
    } catch (err) {
      if (err.isNetworkError) {
        setFeedback({
          type: 'network-error',
          message:
            'Transfer request could not be confirmed due to a network connection failure. The transfer may or may not have been processed by the server. Please check current stock and transfer history before attempting to submit again.',
        })
      } else {
        setFeedback({
          type: 'error',
          message: err.message || 'The stock transfer could not be completed.',
        })
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <section className="app-section" aria-labelledby="transfer-heading">
      <div className="section-header">
        <div>
          <h2 id="transfer-heading" className="section-title">
            New transfer
          </h2>
          <p className="section-subtitle">
            Transfer stock between branches immediately.
          </p>
        </div>
      </div>

      {feedback && (
        <div
          className={`feedback-banner ${
            feedback.type === 'success'
              ? 'feedback-success'
              : feedback.type === 'network-error'
              ? 'feedback-warning'
              : 'feedback-error'
          }`}
          role={feedback.type === 'success' ? 'status' : 'alert'}
          aria-live={feedback.type === 'success' ? 'polite' : 'assertive'}
        >
          <p className="feedback-message">{feedback.message}</p>
          {feedback.refreshWarning && (
            <p className="feedback-submessage">{feedback.refreshWarning}</p>
          )}
        </div>
      )}

      <form className="transfer-form" onSubmit={handleSubmit} noValidate>
        <div className="form-grid">
          {/* From Branch */}
          <div className="form-group">
            <label htmlFor="fromBranch" className="form-label">
              From branch
            </label>
            <select
              id="fromBranch"
              name="fromBranch"
              className={`form-select ${validationErrors.fromBranchId ? 'input-error' : ''}`}
              value={fromBranchId}
              onChange={handleFromBranchChange}
              disabled={isSubmitting || branches.length === 0}
              aria-describedby={validationErrors.fromBranchId ? 'fromBranch-error' : undefined}
              aria-invalid={!!validationErrors.fromBranchId}
            >
              <option value="">Select source branch...</option>
              {branches.map((b) => (
                <option key={b.id} value={b.id}>
                  {b.name}
                </option>
              ))}
            </select>
            {validationErrors.fromBranchId && (
              <span id="fromBranch-error" className="field-error" role="alert">
                {validationErrors.fromBranchId}
              </span>
            )}
          </div>

          {/* To Branch */}
          <div className="form-group">
            <label htmlFor="toBranch" className="form-label">
              To branch
            </label>
            <select
              id="toBranch"
              name="toBranch"
              className={`form-select ${validationErrors.toBranchId ? 'input-error' : ''}`}
              value={toBranchId}
              onChange={handleToBranchChange}
              disabled={isSubmitting || branches.length === 0}
              aria-describedby={validationErrors.toBranchId ? 'toBranch-error' : undefined}
              aria-invalid={!!validationErrors.toBranchId}
            >
              <option value="">Select destination branch...</option>
              {branches.map((b) => {
                const isSameAsSource = String(b.id) === fromBranchId
                return (
                  <option key={b.id} value={b.id} disabled={isSameAsSource}>
                    {b.name} {isSameAsSource ? '(Source)' : ''}
                  </option>
                )
              })}
            </select>
            {validationErrors.toBranchId && (
              <span id="toBranch-error" className="field-error" role="alert">
                {validationErrors.toBranchId}
              </span>
            )}
          </div>

          {/* Product */}
          <div className="form-group">
            <label htmlFor="product" className="form-label">
              Product
            </label>
            <select
              id="product"
              name="product"
              className={`form-select ${validationErrors.productId ? 'input-error' : ''}`}
              value={productId}
              onChange={handleProductChange}
              disabled={isSubmitting || products.length === 0}
              aria-describedby={validationErrors.productId ? 'product-error' : undefined}
              aria-invalid={!!validationErrors.productId}
            >
              <option value="">Select product...</option>
              {products.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name} ({p.packageSize})
                </option>
              ))}
            </select>
            {validationErrors.productId && (
              <span id="product-error" className="field-error" role="alert">
                {validationErrors.productId}
              </span>
            )}
          </div>

          {/* Quantity */}
          <div className="form-group">
            <label htmlFor="quantity" className="form-label">
              Quantity (packages)
            </label>
            <div className="quantity-input-wrapper">
              <input
                id="quantity"
                name="quantity"
                type="text"
                role="spinbutton"
                inputMode="numeric"
                pattern="[0-9]*"
                aria-valuenow={quantity && !isNaN(Number(quantity)) ? Number(quantity) : undefined}
                aria-valuemin={1}
                aria-valuemax={MAX_QUANTITY}
                className={`form-input quantity-input ${validationErrors.quantity ? 'input-error' : ''}`}
                placeholder="e.g. 5"
                value={quantity}
                onChange={handleQuantityChange}
                onKeyDown={handleQuantityKeyDown}
                disabled={isSubmitting}
                aria-describedby={
                  validationErrors.quantity
                    ? 'quantity-error'
                    : availabilityHint
                    ? 'quantity-hint'
                    : undefined
                }
                aria-invalid={!!validationErrors.quantity}
              />
              <div className="quantity-steppers">
                <button
                  type="button"
                  className="stepper-btn stepper-up"
                  aria-label="Increase quantity"
                  onClick={handleStepUp}
                  disabled={isIncrementDisabled}
                >
                  <svg width="10" height="6" viewBox="0 0 10 6" fill="none" aria-hidden="true">
                    <path
                      d="M1 5L5 1L9 5"
                      stroke="currentColor"
                      strokeWidth="1.5"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                    />
                  </svg>
                </button>
                <button
                  type="button"
                  className="stepper-btn stepper-down"
                  aria-label="Decrease quantity"
                  onClick={handleStepDown}
                  disabled={isDecrementDisabled}
                >
                  <svg width="10" height="6" viewBox="0 0 10 6" fill="none" aria-hidden="true">
                    <path
                      d="M1 1L5 5L9 1"
                      stroke="currentColor"
                      strokeWidth="1.5"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                    />
                  </svg>
                </button>
              </div>
            </div>
            {validationErrors.quantity ? (
              <span id="quantity-error" className="field-error" role="alert">
                {validationErrors.quantity}
              </span>
            ) : availabilityHint ? (
              <span id="quantity-hint" className="field-hint">
                {availabilityHint}
              </span>
            ) : null}
          </div>
        </div>

        <div className="form-actions">
          <button
            type="submit"
            className="btn btn-primary"
            disabled={isSubmitting || branches.length === 0 || products.length === 0}
          >
            {isSubmitting ? 'Transferring...' : 'Transfer stock'}
          </button>
        </div>
      </form>
    </section>
  )
}

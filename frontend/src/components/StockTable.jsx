export default function StockTable({
  branches,
  products,
  stockByBranch,
  isLoading,
  error,
  onRefresh,
}) {
  const hasLoadedData = branches.length > 0 && products.length > 0

  return (
    <section className="app-section" aria-labelledby="stock-heading">
      <div className="section-header">
        <div>
          <h2 id="stock-heading" className="section-title">
            Branch stock
          </h2>
          <p className="section-subtitle">
            Current package quantities across all Mihira branch stores.
          </p>
        </div>
        <button
          type="button"
          className="btn btn-secondary"
          onClick={onRefresh}
          disabled={isLoading}
          aria-label="Refresh stock levels and transfer history"
        >
          {isLoading ? 'Refreshing...' : 'Refresh'}
        </button>
      </div>

      {error && !hasLoadedData && (
        <div className="feedback-banner feedback-error" role="alert">
          <p className="feedback-message">{error}</p>
          <button
            type="button"
            className="btn btn-secondary btn-sm"
            onClick={onRefresh}
          >
            Retry
          </button>
        </div>
      )}

      {isLoading && !hasLoadedData && (
        <div className="state-card" aria-busy="true" aria-live="polite">
          <p>Loading branch stock data...</p>
        </div>
      )}

      {!isLoading && !error && products.length === 0 && (
        <div className="state-card">
          <p>No products available.</p>
        </div>
      )}

      {hasLoadedData && (
        <div className="table-responsive">
          <table className="data-table">
            <thead>
              <tr>
                <th scope="col" className="col-product">
                  Product
                </th>
                {branches.map((branch) => (
                  <th key={branch.id} scope="col" className="col-quantity">
                    {branch.name}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {products.map((product) => (
                <tr key={product.id}>
                  <th scope="row" className="row-product-header">
                    <span className="product-name">{product.name}</span>
                    <span className="product-package">{product.packageSize}</span>
                  </th>
                  {branches.map((branch) => {
                    const quantity = stockByBranch[branch.id]?.[product.id]
                    const hasQuantity = typeof quantity === 'number'

                    return (
                      <td
                        key={branch.id}
                        className={`col-quantity ${hasQuantity && quantity === 0 ? 'zero-stock' : ''}`}
                      >
                        {hasQuantity ? (
                          quantity
                        ) : (
                          <span className="unavailable-stock" title="Stock level unavailable">
                            —
                          </span>
                        )}
                      </td>
                    )
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}

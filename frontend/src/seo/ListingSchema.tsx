import type { ListingDetails } from '../api/listing'

/**
 * Розмітка оголошення за словником Schema.org.
 *
 * Це те, що робить із посилання не просто синій рядок у видачі, а картку з
 * ціною, роком і пробігом. Google цей блок читає — він виконує JavaScript,
 * — а месенджери ні, тож картку для них складає сервер окремо.
 *
 * Тип «Car» точніший за загальний «Product»: у нього є саме ті поля, які
 * має авто, і пошуковик розуміє їх без здогадок.
 */
export function ListingSchema({ listing }: { listing: ListingDetails }) {
  const schema = {
    '@context': 'https://schema.org',
    '@type': 'Car',
    name: listing.title,
    description: listing.description,
    brand: { '@type': 'Brand', name: listing.car.make },
    model: listing.car.model,
    vehicleModelDate: String(listing.car.year),
    productionDate: String(listing.car.year),

    ...(listing.car.mileage !== null && {
      mileageFromOdometer: {
        '@type': 'QuantitativeValue',
        value: listing.car.mileage,
        unitCode: 'KMT',
      },
    }),

    ...(listing.car.vin && { vehicleIdentificationNumber: listing.car.vin }),

    ...(listing.photos.length > 0 && {
      image: listing.photos.map((photo) => `/media/${photo.path}`),
    }),

    offers: {
      '@type': 'Offer',
      price: listing.price,
      priceCurrency: listing.currency.toUpperCase(),

      // Продане авто лишається в пошуку, але покупцеві має бути одразу
      // видно, що воно вже не продається.
      availability:
        listing.status === 'Sold'
          ? 'https://schema.org/SoldOut'
          : 'https://schema.org/InStock',
    },
  }

  /*
    dangerouslySetInnerHTML тут доречний, і назва не має лякати: усередині
    тега <script> React не може будувати вузли звичайним способом, а вміст
    ми не беремо від користувача — це наш власний об'єкт, пропущений через
    JSON.stringify, який екранує все, що треба.
  */
  return (
    <script
      type="application/ld+json"
      dangerouslySetInnerHTML={{ __html: JSON.stringify(schema) }}
    />
  )
}

// Optimized from design/GoGoa Rentals  Logo.png (768px WebP); regenerate if the logo changes.
export const LOGO_URL = '/logo.webp'
export const BRAND_NAME = 'GoGoa Rentals'

// Indian mobile number without the country code; +91 is added wherever it is shown or dialled.
export const CONTACT_PHONE = '7709788010'

// WhatsApp number that receives booking requests (until online booking ships). Same as the contact number for now.
export const BOOKING_WHATSAPP = CONTACT_PHONE

// Shortest rental we accept, in rental days (9 am to 9 am). Shown in the header; Book is blocked below it.
export const MIN_RENTAL_DAYS = 2

// Lowest "starting from" daily price (two-wheelers), shown in the hero and above the fleet cards.
export const STARTING_PRICE_PER_DAY = 500

// Fixed refundable deposit per booking, whatever the number of days. Display only: the server charges
// BookingPolicy:TwoWheelerDeposit / FourWheelerDeposit, so keep these in step with appsettings.json.
export const TWO_WHEELER_DEPOSIT = 1500
export const FOUR_WHEELER_DEPOSIT = 4000
